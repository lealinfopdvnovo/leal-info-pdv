import unittest
from release_policy import validate_release, CLIENTS
class PolicyTests(unittest.TestCase):
    def pet(self):
        return dict(Component="PET",TargetClientCode="003",Version="1.2",Sha256="a"*64)
    def test_pet_valid(self):
        self.assertTrue(validate_release(self.pet(),["Extensions/PET/manifest.json","Extensions/PET/Casa.dll"],["clients/003/pet/version.json"],["003"]))
    def test_pet_rejects_base_overwrite(self):
        with self.assertRaises(ValueError):validate_release(self.pet(),["LealInfoPDV.dll"],["clients/003/pet/version.json"],["003"])
    def test_pet_rejects_global_feed(self):
        with self.assertRaises(ValueError):validate_release(self.pet(),["Extensions/PET/manifest.json","Extensions/PET/Casa.dll"],["version.json"],["003"])
    def test_pet_rejects_other_client(self):
        with self.assertRaises(ValueError):validate_release(self.pet(),["Extensions/PET/manifest.json","Extensions/PET/Casa.dll"],["clients/003/pet/version.json"],["001"])
    def test_pet_rejects_base_number(self):
        m=self.pet();m["Version"]="10.466"
        with self.assertRaises(ValueError):validate_release(m,["Extensions/PET/manifest.json","Extensions/PET/Casa.dll"],["clients/003/pet/version.json"],["003"])
    def test_global_valid(self):
        self.assertTrue(validate_release(dict(Component="GLOBAL",Version="10.463",Sha256="a"*64),["LealInfoPDV.dll"],["version.json"],CLIENTS))
    def test_global_rejects_pet_overwrite(self):
        with self.assertRaises(ValueError):validate_release(dict(Component="GLOBAL",Version="10.463",Sha256="a"*64),["Extensions/PET/Casa.dll"],["version.json"],CLIENTS)
    def test_global_rejects_single_client(self):
        with self.assertRaises(ValueError):validate_release(dict(Component="GLOBAL",Version="10.463",Sha256="a"*64),["LealInfoPDV.dll"],["version.json"],["003"])
    def test_global_rejects_target(self):
        with self.assertRaises(ValueError):validate_release(dict(Component="GLOBAL",TargetClientCode="003",Version="10.463",Sha256="a"*64),["LealInfoPDV.dll"],["version.json"],CLIENTS)
    def test_unsafe_path(self):
        with self.assertRaises(ValueError):validate_release(self.pet(),["Extensions/PET/../LealInfoPDV.dll"],["clients/003/pet/version.json"],["003"])
if __name__=="__main__":unittest.main()
