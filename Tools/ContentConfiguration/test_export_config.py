import copy
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
        self.assertEqual(len(export(ROOT / 'Config/GameContent.xlsx', check=True)), 7)

    def test_product_edit_changes_runtime_projection(self):
        tables = copy.deepcopy(self.tables)
        tables['攻击'][1]['damage'] = 37
        self.assertIn('"attack": 37', build_documents(tables)['battle.json'])
        tables['建筑'][0]['maxHealth'] = 999
        self.assertIn('"maxHealth": 999', build_documents(tables)['facilities.json'])

    def test_duplicate_ids_and_missing_references_are_rejected(self):
        for sheet, field, value in [('单位', 'id', self.tables['单位'][1]['id']), ('单位', 'attackId', 'missing'), ('动画绑定', 'resourceId', 'missing')]:
            tables = copy.deepcopy(self.tables)
            tables[sheet][0][field] = value
            with self.assertRaises(ConfigurationError):
                build_documents(tables)

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
