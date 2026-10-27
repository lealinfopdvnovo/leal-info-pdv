using LealInfo.IFood.Groceries;
using System.Globalization;

static class ScenarioInspectionTests
{
    public static async Task<int> Run()
    {
        int passed=0;void Check(bool ok,string label){if(!ok)throw new Exception("FAIL inspection: "+label);Console.WriteLine("PASS inspection: "+label);passed++;}
        bool Reject(Action action){try{action();return false;}catch(ReceptionFailure){return true;}}
        var merchant=Guid.NewGuid();var order=Guid.NewGuid();var ids=new[]{Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid()};
        var plan=ScenarioSelection.Create(order,merchant,ids[0],ids[1],ids[2]);
        // Obtain the real portable model through its strict parser, not a parallel test-only validator.
        var json=System.Text.Json.JsonSerializer.Serialize(new{id=order,merchant=new{id=merchant},test=true,category="GROCERY",
            bag=new{items=ids.Select((id,n)=>new{uniqueId=id,name="FAKE_PRIVATE_PRODUCT_"+n,quantity=n==2?1:3,unavailable=n==0,product=new{isVariableWeight=false},prices=new{unitValue=new{value=1000},grossValue=new{value=n==2?1000:3000}}}),prices=new{grossValue=new{value=4000}}},
            operationMode=new{type="DELIVERY",schedulingType="IMMEDIATE",delivery=new{}},payment=new{methods=new[]{new{amount=new{value=3500}}}},
            benefit=new{benefits=new[]{new{target="CART",sponsorships=new[]{new{liability="OWN",amount=new{value=500}}}}}}});
        BagOrder Bag()=>PortableBag.Parse(json,order,merchant);
        var bag=Bag();var pending=ScenarioInspection.Inspect(bag,null);
        Check(!pending.QuantitiesMatch && pending.Criteria.Count(c=>c.QuantityCriterion && c.State==InspectionState.Pending)==3,"unmapped items remain pending; no inference from array order");
        var review=ScenarioInspection.Inspect(bag,plan);
        Check(review.QuantitiesMatch&&review.Criteria.Count==10&&review.Criteria.Count(c=>c.State==InspectionState.Pass)==4,"four quantitative criteria pass and ten criteria are visible");
        Check(review.Criteria.Count(c=>c.State==InspectionState.Pending)==5,"original 3x3, coupon, cancellation request, video and official logs remain pending");
        Check(review.Criteria.Single(c=>c.Title=="Beneficios da sacola").State==InspectionState.Observed,"discount is observed, never proof of original coupon");
        var reverseJson=json.Replace("FAKE_PRIVATE_PRODUCT_0","FAKE_PRIVATE_PRODUCT_A");var reversed=PortableBag.Parse(reverseJson,order,merchant);
        System.Array.Reverse(reversed.Items);
        Check(ScenarioInspection.Inspect(reversed,plan).QuantitiesMatch,"mapping by uniqueId remains valid after server reorders products");
        Check(Reject(()=>ScenarioSelection.Create(order,merchant,ids[0],ids[0],ids[2])),"duplicate role assignment rejected");
        Check(Reject(()=>ScenarioSelection.Create(order,merchant,Guid.Empty,ids[1],ids[2])),"missing role assignment rejected");
        Check(Reject(()=>ScenarioInspection.Inspect(bag,ScenarioSelection.Create(Guid.NewGuid(),merchant,ids[0],ids[1],ids[2]))),"plan from another order rejected");
        Check(Reject(()=>ScenarioInspection.Inspect(bag,ScenarioSelection.Create(order,Guid.NewGuid(),ids[0],ids[1],ids[2]))),"plan from another merchant rejected");
        var wrongRole=ScenarioSelection.Create(order,merchant,ids[1],ids[0],ids[2]);
        Check(!ScenarioInspection.Inspect(bag,wrongRole).QuantitiesMatch,"swapping cancellation and three-unit roles fails without reordering source items");
        var missingRole=ScenarioSelection.Create(order,merchant,Guid.NewGuid(),ids[1],ids[2]);var missing=ScenarioInspection.Inspect(bag,missingRole);
        Check(!missing.QuantitiesMatch&&missing.Criteria.Any(c=>c.Detail.Contains("ausente")&&c.State==InspectionState.Fail),"missing expected product fails instead of implying cancellation");
        var zero=PortableBag.Parse(json.Replace("\"value\":500","\"value\":0"),order,merchant);
        Check(ScenarioInspection.Inspect(zero,plan).QuantitiesMatch&&ScenarioInspection.Inspect(zero,plan).Criteria.Count(c=>c.State==InspectionState.Pending)==5,"zero final discount preserves quantitative result while coupon evidence remains pending");
        string summary=review.Summary(bag,plan,false);
        Check(summary.Contains(order.ToString())&&ids.All(id=>summary.Contains(id.ToString()))&&summary.Contains("0/3/1 CONFORME PLANO LOCAL"),"per-order summary includes identified products and quantitative result");
        Check(summary.Contains("PENDENTE")&&summary.Contains("Nenhum resultado desta conferencia equivale a aprovacao do iFood"),"summary never claims official homologation");
        Check(!summary.Contains("FAKE_PRIVATE_PRODUCT")&&!summary.Contains("Authorization")&&!summary.Contains("clientSecret"),"summary excludes product names, HTTP headers and credential fields");
        Check(review.Summary(bag,plan,true).Contains("SIMULACAO LOCAL - SEM API, SEM HOMOLOGACAO"),"offline interface summary carries explicit simulation origin");
        Check(review.Summary(bag,null,false).Contains("PENDENTE OU DIVERGENTE"),"summary recalculates instead of reusing an earlier passing selection");
        var culture=CultureInfo.CurrentCulture;
        try{CultureInfo.CurrentCulture=new CultureInfo("pt-BR");Check(review.Summary(bag,plan,false).Contains("sacola=4000 | pagamento=3500"),"technical cent amounts independent of Windows culture");}
        finally{CultureInfo.CurrentCulture=culture;}
        var root=Path.Combine(Path.GetTempPath(),"ifood-review-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            var store=new ScenarioSelectionStore(root,merchant);Check(store.Find(order)==null,"new order has no silently selected roles");
            store.Save(plan);var reopened=new ScenarioSelectionStore(root,merchant);var saved=reopened.Find(order)!;
            Check(saved.CancelItem==ids[0]&&saved.ThreeItem==ids[1]&&saved.OneItem==ids[2],"roles survive reopening with correct order/merchant scope");
            Check(ScenarioInspection.Inspect(bag,saved).QuantitiesMatch,"restored plan produces same visible quantitative criteria");
            Check(store.Find(Guid.NewGuid())==null,"another order cannot reuse selected roles");
            Check(Reject(()=>store.Save(ScenarioSelection.Create(order,Guid.NewGuid(),ids[0],ids[1],ids[2]))),"foreign merchant cannot persist a plan");
            Check(Reject(()=>new ScenarioSelectionStore(root,Guid.NewGuid())),"opening selection file for another merchant fails closed");
            string text=File.ReadAllText(Path.Combine(root,"scenario-selections-v1.json"));
            Check(!text.Contains("FAKE_PRIVATE_PRODUCT")&&!text.Contains("clientSecret")&&!text.Contains("customer"),"selection file holds technical IDs only");
            Directory.CreateDirectory(Path.Combine(root,"scenario-selections-v1.json.tmp"));bool failed=false;
            try{store.Save(wrongRole);}catch(IOException){failed=true;}catch(UnauthorizedAccessException){failed=true;}
            Check(failed&&store.Find(order)!.CancelItem==ids[0]&&new ScenarioSelectionStore(root,merchant).Find(order)!.CancelItem==ids[0],"disk failure preserves prior in-memory and durable selection");
            Directory.Delete(Path.Combine(root,"scenario-selections-v1.json.tmp"));
            await Task.WhenAll(Enumerable.Range(0,12).Select(n=>Task.Run(()=>store.Save(n%2==0?plan:wrongRole))));
            var after=new ScenarioSelectionStore(root,merchant).Find(order)!;
            Check(after.CancelItem==ids[0]||after.CancelItem==ids[1],"concurrent plan saves remain atomically readable");
            string corrupt=Path.Combine(root,"corrupt");Directory.CreateDirectory(corrupt);File.WriteAllText(Path.Combine(corrupt,"scenario-selections-v1.json"),"broken");
            Check(Reject(()=>new ScenarioSelectionStore(corrupt,merchant))&&File.ReadAllText(Path.Combine(corrupt,"scenario-selections-v1.json"))=="broken","corrupt selection file preserved for analysis");
        }
        finally{Directory.Delete(root,true);}
        Console.WriteLine("INSPECTION TOTAL "+passed+" PASS; offline only, no Windows UI or live API execution.");return passed;
    }
}
