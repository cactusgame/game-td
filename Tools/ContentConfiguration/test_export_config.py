import copy
import json
import tempfile
import unittest
from pathlib import Path
import xml.etree.ElementTree as ET
import zipfile
from export_config import ROOT, NS, ConfigurationError, read_workbook, build_documents, export


class ExportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tables = read_workbook(ROOT / 'Config/GameContent.xlsx')

    def test_workbook_matches_all_generated_files(self):
        self.assertEqual(len(export(ROOT / 'Config/GameContent.xlsx', check=True)), 8)

    def test_product_edit_changes_runtime_projection(self):
        tables = copy.deepcopy(self.tables)
        tables['攻击'][1]['damage'] = 37
        self.assertIn('"attack": 37', build_documents(tables)['battle.json'])
        tables['建筑'][0]['maxHealth'] = 999
        self.assertIn('"maxHealth": 999', build_documents(tables)['facilities.json'])

    def test_duplicate_ids_and_missing_references_are_rejected(self):
        for sheet, field, value in [('单位', 'id', self.tables['单位'][1]['id']), ('单位', 'attackId', 'missing'), ('单位', 'animationResourceId', 'missing')]:
            tables = copy.deepcopy(self.tables)
            tables[sheet][0][field] = value
            with self.assertRaises(ConfigurationError):
                build_documents(tables)

    def test_animation_resource_is_selected_once_and_projects_without_binding_table(self):
        tables = copy.deepcopy(self.tables)
        tables['单位'][0]['animationResourceId'] = tables['单位'][1]['animationResourceId']
        documents = build_documents(tables)
        units = json.loads(documents['presentation.json'])['Units']
        self.assertEqual(units[0]['ResourceId'], tables['单位'][1]['animationResourceId'])
        self.assertNotIn('animations', json.loads(documents['combat-content.json']))
        self.assertNotIn('animationId', json.loads(documents['combat-content.json'])['units'][0])

    def test_fortification_health_is_exported_and_must_be_positive_integer(self):
        for key in ('wallHealth', 'gateHealth'):
            tables = copy.deepcopy(self.tables)
            record = next(r for r in tables['全局参数'] if r['id'] == 'facilities.wall.' + key)
            record['value'] = '987'
            wall = json.loads(build_documents(tables)['facilities.json'])['wall']
            self.assertEqual(wall[key], 987)
            for invalid in ('0', '1.5'):
                record['value'] = invalid
                with self.assertRaises(ConfigurationError):
                    build_documents(tables)

    def test_unknown_attack_mode_is_rejected(self):
        tables = copy.deepcopy(self.tables)
        tables['攻击'][0]['mode'] = 'Ranged'
        with self.assertRaises(ConfigurationError):
            build_documents(tables)

    def test_barracks_references_capacity_and_training_are_validated(self):
        for field, value in [('unitId', 'enemy.skeleton'), ('capacity', 0), ('capacity', 101), ('retrainSeconds', 0)]:
            tables = copy.deepcopy(self.tables)
            tables['兵营'][0][field] = value
            with self.assertRaises(ConfigurationError):
                build_documents(tables)

    def test_barracks_and_economy_project_into_their_own_content(self):
        tables = copy.deepcopy(self.tables)
        tables['兵营'][0]['capacity'] = 5
        document = json.loads(build_documents(tables)['construction-content.json'])
        self.assertEqual(document['barracks'][0]['capacity'], 5)
        units = json.loads(build_documents(tables)['battle.json'])
        self.assertEqual(units['soldier']['id'], 'ally.soldier')
        self.assertFalse(units['soldier']['canAttackGates'])
        self.assertEqual(json.loads(build_documents(tables)['economy.json'])['initialGold'], 1000)

    def test_upgrade_cycle_and_cross_building_dependency_are_rejected(self):
        tables = copy.deepcopy(self.tables)
        tables['升级分支'][0]['prerequisiteId'] = tables['升级分支'][1]['id']
        tables['升级分支'][1]['prerequisiteId'] = tables['升级分支'][0]['id']
        with self.assertRaises(ConfigurationError):
            build_documents(tables)

    def test_invalid_numeric_values_are_rejected(self):
        for sheet, field, value in [('攻击', 'interval', 0), ('建筑', 'refundRatio', 2), ('单位', 'maxHealth', 0), ('效果', 'magnitude', 2)]:
            tables = copy.deepcopy(self.tables)
            tables[sheet][0][field] = value
            with self.assertRaises(ConfigurationError):
                build_documents(tables)

    def test_formula_rejection_preserves_existing_output(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            workbook = folder / 'invalid.xlsx'
            source = ROOT / 'Config/GameContent.xlsx'
            with zipfile.ZipFile(source) as incoming, zipfile.ZipFile(workbook, 'w') as outgoing:
                for entry in incoming.infolist():
                    payload = incoming.read(entry.filename)
                    if entry.filename == 'xl/worksheets/sheet2.xml':
                        xml = ET.fromstring(payload)
                        cell = next(c for c in xml.findall('.//x:c', NS) if c.attrib['r'] == 'D3')
                        ET.SubElement(cell, '{' + NS['x'] + '}f').text = '1+1'
                        payload = ET.tostring(xml)
                    outgoing.writestr(entry, payload)
            output = folder / 'output'
            output.mkdir()
            (output / 'battle.json').write_text('previous valid configuration')
            with self.assertRaises(ConfigurationError):
                export(workbook, output)
            self.assertEqual((output / 'battle.json').read_text(), 'previous valid configuration')


if __name__ == '__main__':
    unittest.main()
