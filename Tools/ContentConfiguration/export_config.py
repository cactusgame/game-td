"""Read the designer workbook using Python's standard library; validate before publishing any JSON."""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Assets/Content/Configuration/Resources/GameConfig'
NS = {'x': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}


class ConfigurationError(ValueError):
    pass


def read_workbook(path):
    schema = json.loads((Path(__file__).with_name('schema.json')).read_text(encoding='utf-8'))
    with zipfile.ZipFile(path) as archive:
        shared = []
        if 'xl/sharedStrings.xml' in archive.namelist():
            shared = [''.join(e.itertext()) for e in ET.fromstring(archive.read('xl/sharedStrings.xml')).findall('x:si', NS)]
        relations = {e.attrib['Id']: e.attrib['Target'] for e in ET.fromstring(archive.read('xl/_rels/workbook.xml.rels'))}
        result = {}
        for sheet in ET.fromstring(archive.read('xl/workbook.xml')).findall('x:sheets/x:sheet', NS):
            name = sheet.attrib['name']
            if name not in schema:
                continue
            rid = sheet.attrib['{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id']
            target = relations[rid]
            location = target.lstrip('/') if target.startswith('/') else 'xl/' + target
            rows = {}
            for row in ET.fromstring(archive.read(location)).findall('x:sheetData/x:row', NS):
                cells = {}
                for cell in row.findall('x:c', NS):
                    address = cell.attrib['r']
                    column = ''.join(c for c in address if c.isalpha())
                    value = cell.find('x:v', NS)
                    value = value.text if value is not None else ''
                    kind = cell.attrib.get('t')
                    if cell.find('x:f', NS) is not None:
                        raise ConfigurationError(f'{name}!{address}: 不接受公式，请填写明确值')
                    if kind == 's':
                        value = shared[int(value)]
                    elif kind == 'inlineStr':
                        value = ''.join(cell.find('x:is', NS).itertext())
                    elif kind == 'b':
                        value = value == '1'
                    elif kind == 'e':
                        raise ConfigurationError(f'{name}!{address}: Excel错误值 {value}')
                    cells[column] = value
                rows[int(row.attrib['r'])] = cells
            headers = rows.get(2, {})
            if len(set(headers.values())) != len(headers):
                raise ConfigurationError(f'{name}: 字段名重复')
            columns = {str(v): k for k, v in headers.items()}
            expected = {c['key'] for c in schema[name]}
            if set(columns) != expected:
                raise ConfigurationError(f'{name}: 字段不匹配，应为 {sorted(expected)}')
            records = []
            for row_number, cells in sorted(rows.items()):
                if row_number < 3 or not any(v != '' for v in cells.values()):
                    continue
                record = {'_source': f'{name}!{row_number}'}
                for field in schema[name]:
                    key = field['key']
                    raw = cells.get(columns[key], '')
                    try:
                        if field['type'] in ('number', 'int'):
                            value = float(raw)
                            if not math.isfinite(value) or value < 0:
                                raise ValueError('必须为非负有限数')
                            if field['type'] == 'int' and value != int(value):
                                raise ValueError('必须为整数')
                            value = int(value) if field['type'] == 'int' else value
                        elif field['type'] == 'bool':
                            if str(raw).lower() not in ('true', 'false', '1', '0'):
                                raise ValueError('必须为TRUE或FALSE')
                            value = str(raw).lower() in ('true', '1')
                        else:
                            value = str(raw).strip()
                    except (ValueError, OverflowError) as error:
                        raise ConfigurationError(f'{name}!{columns[key]}{row_number} ({key}): {error}') from error
                    record[key] = value
                records.append(record)
            if not records and name not in ('技能', '效果', '升级分支'):
                raise ConfigurationError(f'{name}: 至少需要一条配置')
            result[name] = records
        missing = set(schema) - set(result)
        if missing:
            raise ConfigurationError(f'缺少工作表: {sorted(missing)}')
    return result


def fail(record, message):
    raise ConfigurationError(f'{record["_source"]}: {message}')


def ids(records):
    result = {}
    for record in records:
        key = record['id']
        if not key or ';' in key or any(c.isspace() for c in key) or key in result:
            fail(record, f'ID为空、含空白或重复: {key}')
        result[key] = record
    return result


def refs(record, field, catalog, multiple=False):
    values = record[field].split(';') if multiple else [record[field]]
    values = [v.strip() for v in values if v.strip()]
    if len(values) != len(set(values)):
        fail(record, f'{field} 引用重复')
    for value in values:
        if value not in catalog:
            fail(record, f'{field} 引用不存在: {value}')
    return values


def clean(record):
    return {k: v for k, v in record.items() if not k.startswith('_')}


def build_documents(tables):
    by = {name: ids(rows) for name, rows in tables.items()}
    attacks, skills, effects = by['攻击'], by['技能'], by['效果']
    behavior, animation, buildings, upgrades = by['怪物行为'], by['动画绑定'], by['建筑'], by['升级分支']
    for record in attacks.values():
        if record['mode'] not in ('Contact', 'Melee', 'Direct') or record['interval'] <= 0:
            fail(record, '攻击模式无效或攻击间隔必须大于0')
        refs(record, 'effectIds', effects, True)
    for record in effects.values():
        if record['kind'] not in ('Slow', 'Damage', 'Heal') or record['duration'] <= 0 or record['maxStacks'] <= 0:
            fail(record, '效果类型、持续时间或层数无效')
        if record['kind'] == 'Slow' and record['magnitude'] > 1:
            fail(record, '减速比例必须在0至1之间')
    for record in skills.values():
        refs(record, 'attackId', attacks)
        refs(record, 'effectIds', effects, True)
        if record['cooldown'] <= 0:
            fail(record, '技能冷却必须大于0')
    for record in behavior.values():
        if record['target'] not in ('MainCastle', 'Hero'):
            fail(record, '目标策略必须为MainCastle或Hero')
    resources = {p.stem for p in (ROOT / 'Assets/Content/Units').rglob('*-actions.asset')}
    for record in animation.values():
        if record['resourceId'] not in resources:
            fail(record, f'动画资源不存在: {record["resourceId"]}')
    slots = {}
    for record in by['单位'].values():
        if record['slot']:
            if record['slot'] not in ('Hero', 'Goblin', 'Runner') or record['slot'] in slots:
                fail(record, '运行槽位应为Hero/Goblin/Runner且不能重复；新增储备单位可留空')
            slots[record['slot']] = record
        for key in ('maxHealth', 'speed', 'radius', 'visualHeight'):
            if record[key] <= 0:
                fail(record, key + '必须大于0')
        for field, catalog in [('attackId', attacks), ('skillIds', skills), ('behaviorId', behavior), ('animationId', animation)]:
            refs(record, field, catalog, field == 'skillIds')
        if not record['attackId'] or not record['animationId'] or ((record['slot'] in ('Goblin', 'Runner') or record['id'].startswith('enemy.')) and not record['behaviorId']):
            fail(record, '单位必须绑定攻击、动画，怪物还必须绑定行为')
    if set(slots) != {'Hero', 'Goblin', 'Runner'}:
        raise ConfigurationError('单位表必须保留全部三个运行槽位')
    for record in buildings.values():
        refs(record, 'attackId', attacks)
        if min(record['width'], record['height'], record['maxHealth']) <= 0 or record['refundRatio'] > 1:
            fail(record, '占地和生命必须为正数，返还比例不得大于1')
    if 'hall' not in buildings or buildings['hall']['canDemolish']:
        raise ConfigurationError('必须保留不可拆除的主堡hall')
    for record in upgrades.values():
        refs(record, 'buildingId', buildings)
        refs(record, 'prerequisiteId', upgrades)
        seen = {record['id']}
        current = record
        while current['prerequisiteId']:
            current = upgrades[current['prerequisiteId']]
            if current['id'] in seen or current['buildingId'] != record['buildingId']:
                fail(record, '升级前置存在循环或跨建筑引用')
            seen.add(current['id'])
    expected = json.loads(Path(__file__).with_name('settings_schema.json').read_text(encoding='utf-8'))
    if set(by['全局参数']) != set(expected):
        raise ConfigurationError('全局参数缺失或增加了不支持的键，请保留原键名')
    documents = {name: {'schemaVersion': version} for name, version in [('battle', 1), ('presentation', 3), ('facilities', 1), ('DefenseEncounter', 1), ('MapPlayCamera', 1)]}
    for record in tables['全局参数']:
        value = record['value']
        try:
            if record['type'] != expected[record['id']]:
                fail(record, '全局参数类型被修改')
            if record['type'] == 'number':
                value = float(value)
                if not math.isfinite(value) or value < 0:
                    raise ValueError('必须为非负有限数')
            elif record['type'] == 'bool':
                if value.lower() not in ('true', 'false'):
                    raise ValueError('必须为TRUE/FALSE')
                value = value.lower() == 'true'
        except ValueError as error:
            fail(record, str(error))
        parts = record['id'].split('.')
        target = documents[parts[0]]
        for part in parts[1:-1]:
            target = target.setdefault(part, {})
        target[parts[-1]] = value
    battle = documents['battle']
    for slot, key in [('Hero', 'hero'), ('Goblin', 'goblin'), ('Runner', 'runner')]:
        record = slots[slot]
        attack = attacks[record['attackId']]
        rule = behavior.get(record['behaviorId'], {'target': 'Hero', 'canAttackGates': False})
        battle[key] = {k: record[k] for k in ('id', 'maxHealth', 'defense', 'speed', 'radius', 'visualHeight')}
        battle[key].update(attack=attack['damage'], attackInterval=attack['interval'], attackRange=attack['range'],
                           targetMainCastle=rule['target'] == 'MainCastle', canAttackGates=rule['canAttackGates'])
    documents['presentation']['Units'] = [{'UnitKind': slot, 'ResourceId': animation[record['animationId']]['resourceId']} for slot, record in slots.items()]
    facilities = []
    for record in buildings.values():
        attack = attacks.get(record['attackId'], {'damage': 0, 'range': 0, 'interval': 1})
        item = {k: record[k] for k in ('id', 'name', 'width', 'height', 'blocksMovement', 'blocksShots', 'maxHealth')}
        item.update(attack=attack['damage'], attackRange=attack['range'], attackInterval=attack['interval'])
        facilities.append(item)
    documents['facilities']['facilities'] = facilities
    documents['combat-content'] = {'schemaVersion': 1, **{key: [clean(r) for r in tables[sheet]] for key, sheet in [('units', '单位'), ('attacks', '攻击'), ('skills', '技能'), ('effects', '效果'), ('behaviors', '怪物行为'), ('animations', '动画绑定')]}}
    documents['construction-content'] = {'schemaVersion': 1, 'buildings': [clean(r) for r in tables['建筑']], 'upgrades': [clean(r) for r in tables['升级分支']]}
    nav = battle['navigation']
    if nav['loseTargetRadius'] < nav['detectionRadius'] or min(nav['cellSize'], nav['repathSeconds'], nav['separationSpeed']) <= 0:
        raise ConfigurationError('全局导航参数无效')
    if not 0 < battle['fixedStep'] <= 0.1 or documents['MapPlayCamera']['visibleHeight'] <= 0:
        raise ConfigurationError('模拟步长或摄影机视野无效')
    if min(battle['arena']['width'], battle['arena']['height']) <= max(r['radius'] for r in slots.values()) * 2 + 2:
        raise ConfigurationError('测试场地尺寸不足以容纳单位')
    if nav['detectionRadius'] <= 0:
        raise ConfigurationError('感知半径必须大于0')
    for name, version in [('battle', 1), ('presentation', 3), ('facilities', 1), ('DefenseEncounter', 1), ('MapPlayCamera', 1)]:
        if documents[name]['schemaVersion'] != version:
            raise ConfigurationError(name + '的schemaVersion不能修改')
        documents[name]['schemaVersion'] = version
    encounter = documents['DefenseEncounter']
    for key in ('firstWaveCount', 'secondWaveCount'):
        value = encounter[key]
        if value != int(value) or not 0 < value <= 100000:
            raise ConfigurationError('波次数量必须为1至100000的整数')
        encounter[key] = int(value)
    if encounter['waveInterval'] <= 0:
        raise ConfigurationError('波次间隔必须大于0')
    wall = documents['facilities']['wall']
    if min(wall['width'], wall['height'], wall['junctionRadius'], wall['gateAttackInterval'], wall['gateHealth']) <= 0 or wall['junctionRadius'] <= wall['width'] * 0.5:
        raise ConfigurationError('城墙尺寸、门生命或攻击间隔无效')
    if wall['gateHealth'] != int(wall['gateHealth']):
        raise ConfigurationError('门生命必须为整数')
    wall['gateHealth'] = int(wall['gateHealth'])
    for color in ('GroundLight', 'GroundDark', 'BorderColor'):
        if any(v > 1 for v in documents['presentation']['Map'][color].values()):
            raise ConfigurationError('表现颜色分量必须为0至1')
    for key in ('seed',):
        if battle[key] != int(battle[key]):
            raise ConfigurationError(key + '必须为整数')
        battle[key] = int(battle[key])
    for key in battle['spawn']:
        value = battle['spawn'][key]
        if value != int(value):
            raise ConfigurationError('怪物数量必须为整数')
        battle['spawn'][key] = int(value)
    version_payload = {name: [clean(row) for row in records] for name, records in tables.items()}
    version = hashlib.sha256(json.dumps(version_payload, ensure_ascii=False, sort_keys=True).encode('utf-8')).hexdigest()[:16]
    for document in documents.values():
        document['contentVersion'] = version
    # Domain-specific loaders perform a second validation at the engine boundary.
    return {name + '.json': json.dumps(content, ensure_ascii=False, indent=2, allow_nan=False) + '\n' for name, content in documents.items()}


def export(workbook, output=OUTPUT, check=False):
    documents = build_documents(read_workbook(workbook))
    if check:
        stale = [name for name, text in documents.items() if not (output / name).exists() or (output / name).read_text(encoding='utf-8') != text]
        if stale:
            raise ConfigurationError('JSON未与Excel同步: ' + ', '.join(stale))
        return documents
    output.mkdir(parents=True, exist_ok=True)
    old = {name: (output / name).read_bytes() if (output / name).exists() else None for name in documents}
    changed = []
    with tempfile.TemporaryDirectory(prefix='game-td-config-') as temporary:
        stage = Path(temporary)
        for name, text in documents.items():
            (stage / name).write_text(text, encoding='utf-8')
        try:
            for name in documents:
                # Staging on the destination filesystem keeps each replacement atomic.
                pending = output / (name + '.pending')
                pending.write_bytes((stage / name).read_bytes())
                os.replace(pending, output / name)
                changed.append(name)
        except Exception:
            for name in changed:
                if old[name] is None:
                    (output / name).unlink(missing_ok=True)
                else:
                    (output / name).write_bytes(old[name])
            raise
        finally:
            for name in documents:
                (output / (name + '.pending')).unlink(missing_ok=True)
    return documents


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--workbook', type=Path, default=ROOT / 'Config/GameContent.xlsx')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    try:
        result = export(args.workbook, check=args.check)
        print(f'{"Checked" if args.check else "Exported"} {len(result)} JSON files: {OUTPUT}')
    except (ConfigurationError, OSError, zipfile.BadZipFile) as error:
        parser.exit(1, str(error) + '\n')
