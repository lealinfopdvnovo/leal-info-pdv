import unittest
from copy import deepcopy
from plan_global_pet import plan_global

class ContractTests(unittest.TestCase):
    def setUp(self):
        self.states=[dict(Identity=str(i),Edition=e,CommonVersion='10.442',PetVersion='1.'+str(i),PetSha256=str(i)*64,CustomRules={'preserve':i}) for i,e in enumerate(['STANDARD','PLUS','PRO','PRO'],1)]
    def test_all_editions_receive_common_without_pet_change(self):
        old=deepcopy(self.states)
        p=plan_global(self.states,'10.443')
        self.assertEqual(self.states,old)
        self.assertFalse(p['CanPublish'])
        for x in p['Installations']:
            self.assertEqual(x['Changed'],['CommonVersion'])
            self.assertEqual(x['After'],dict(x['Before'],CommonVersion='10.443'))
    def test_pet_is_independent_per_installation(self):
        self.states[0]['PetVersion']='1.27'
        self.states[1]['PetVersion']='1.2'
        p=plan_global(self.states,'10.443')
        self.assertEqual([x['After']['PetVersion'] for x in p['Installations']],['1.27','1.2','1.3','1.4'])
    def test_reject_downgrade_or_equal(self):
        for v in ['10.441','10.442','10.442.0']:
            with self.assertRaises(ValueError):plan_global(self.states,v)
    def test_no_automatic_legacy_migration(self):
        del self.states[0]['PetVersion']
        with self.assertRaises(KeyError):plan_global(self.states,'10.443')
    def test_reject_duplicate_identity(self):
        self.states[1]['Identity']=self.states[0]['Identity']
        with self.assertRaises(ValueError):plan_global(self.states,'10.443')
    def test_result_cannot_mutate_existing_pet(self):
        p=plan_global(self.states,'10.443')
        p['Installations'][0]['After']['CustomRules']['preserve']=999
        self.assertEqual(self.states[0]['CustomRules']['preserve'],1)

if __name__=='__main__':unittest.main()
