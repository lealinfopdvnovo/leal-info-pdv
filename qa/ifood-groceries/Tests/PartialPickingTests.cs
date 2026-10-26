using LealInfo.IFood.Groceries;
using System.Text.Json;

static class PartialPickingTests
{
    public static async Task<int> Run()
    {
        int passed = 0;
        void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL picking: " + label); Console.WriteLine("PASS picking: " + label); passed++; }
        var order = Guid.NewGuid(); var shop = Guid.NewGuid(); var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var plan = new PartialPickingPlan(order, shop, a, b, c);
        var items = new[] { new Item(a,"Teste 1",3,true,1000,3000),new Item(b,"Teste 2",3,false,1000,3000),new Item(c,"Teste 3",1,false,1000,1000) };
        var bag = new Order(order,shop,"DELIVERY","IMMEDIATE",null,null,items,new[]{new Benefit("CART",null,"OWN",500)},4000,3500);
        Check(plan.Lines.All(l=>l.OrderedQuantity==3) && plan.Lines.Select(l=>l.PlannedQuantity).SequenceEqual(new[]{0,3,1}),"immutable plan preserves three original units and requested 0/3/1 outcome");
        Check(plan.Lines[0].Action==PlannedPickingAction.RequestCancellation,"item 1 records cancellation intent, not remote confirmation");
        var good=plan.Review(bag);Check(good.QuantitiesMatch && good.FinalDiscountPresent,"expected final bag matches scenario and observes discount");
        Check(good.RequiresCouponEvidence && good.RequiresCancellationRequestEvidence,"final bag never claims proof of coupon or cancellation request");
        Check(plan.Review(bag with{Items=items.Reverse().ToArray()}).QuantitiesMatch,"matches by uniqueId, not server array position");
        Check(!plan.Review(bag with{Items=items.Skip(1).ToArray()}).QuantitiesMatch,"missing item 1 is not inferred as cancellation");
        Check(!plan.Review(bag with{Items=items.Append(new Item(Guid.NewGuid(),"Extra",1,false,100,100)).ToArray()}).QuantitiesMatch,"extra product fails the exact three-product scenario");
        Check(!plan.Review(bag with{Items=new[]{items[0] with{Unavailable=false,ReportedQuantity=0},items[1],items[2]}}).QuantitiesMatch,"zero quantity without unavailable fails");
        Check(plan.Review(bag with{Items=new[]{items[0] with{ReportedQuantity=0},items[1],items[2]}}).QuantitiesMatch,"unavailable line can retain zero or original reported quantity");
        Check(!plan.Review(bag with{Items=new[]{items[0],items[1] with{ReportedQuantity=2},items[2]}}).QuantitiesMatch,"item 2 must keep all three units");
        Check(!plan.Review(bag with{Items=new[]{items[0],items[1],items[2] with{ReportedQuantity=3}}}).QuantitiesMatch,"item 3 must keep only one unit");
        Check(!plan.Review(bag with{Items=new[]{items[0],items[1],items[2] with{Unavailable=true}}}).QuantitiesMatch,"partial unit cannot be incorrectly unavailable");
        var noDiscount=plan.Review(bag with{Benefits=Array.Empty<Benefit>()});
        Check(noDiscount.QuantitiesMatch && !noDiscount.FinalDiscountPresent && noDiscount.RequiresCouponEvidence,"loss of final discount is reported without inventing coupon rules");
        foreach(var bad in new[]{bag with{Id=Guid.NewGuid()},bag with{MerchantId=Guid.NewGuid()},bag with{Items=new[]{items[0],items[0],items[2]}}}){
            bool failed=false;try{plan.Review(bad);}catch(ContractException){failed=true;}Check(failed,"foreign identity or duplicate item rejected");
        }
        bool invalid=false;try{_ = new PartialPickingPlan(order,shop,a,a,c);}catch(ArgumentException){invalid=true;}Check(invalid,"duplicate planned product rejected");
        Check(plan.Review(bag with{Operation="TAKEOUT"}).QuantitiesMatch,"same picking check supports takeout");
        Check(plan.Review(bag with{Scheduling="TIME_SLOT",WindowFrom=DateTimeOffset.UtcNow,WindowTo=DateTimeOffset.UtcNow.AddHours(1)}).QuantitiesMatch,"same picking check supports scheduled orders");
        var dir=Path.Combine(Path.GetTempPath(),"ifood-picking-"+Guid.NewGuid()); int reads=0;
        object Money(int value)=>new{value,currency="BRL"};
        var json=JsonSerializer.Serialize(new{id=order,merchant=new{id=shop},test=true,category="GROCERY",
            bag=new{items=items.Select(i=>new{uniqueId=i.Id,name=i.Name,quantity=i.ReportedQuantity,unavailable=i.Unavailable,product=new{isVariableWeight=false},prices=new{unitValue=Money(1000),grossValue=Money((int)i.GrossCents)}}),prices=new{grossValue=Money(4000)}},
            operationMode=new{type="DELIVERY",schedulingType="IMMEDIATE",delivery=new{}},payment=new{methods=new[]{new{amount=Money(3500)}}},
            benefit=new{benefits=new[]{new{target="CART",sponsorships=new[]{new{liability="OWN",amount=Money(500)}}}}}});
        Task<string> Fetch(Guid id,CancellationToken ct){reads++;return Task.FromResult(json);}
        var signal=new Signal(Guid.NewGuid(),order,shop,"RFI");
        try{
            using(var inbox=new Inbox(dir,shop)){
                var processor=new FinalBagPreparation(inbox);
                var early=await processor.ReceiveAsync(signal with{Code="PLC"},plan,Fetch);
                Check(early.Review is null && !early.Receipt.ReadyToAcknowledge && reads==0,"no virtual bag fetch or scenario claim before RFI");
                var ready=await processor.ReceiveAsync(signal,plan,Fetch);
                Check(ready.Review!.QuantitiesMatch && ready.Receipt.ReadyToAcknowledge && reads==1,"RFI fetch, strict unavailable parse, atomic persist and review integrated");
            }
            using(var inbox=new Inbox(dir,shop)){
                var replay=await new FinalBagPreparation(inbox).ReceiveAsync(signal,plan,Fetch);
                Check(replay.Receipt.Duplicate && replay.Review!.QuantitiesMatch && reads==1,"restart recovers persisted bag without duplicate fetch/import");
            }
        }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        Console.WriteLine("PICKING PREPARATION TOTAL "+passed+" PASS; offline only, no live picking commands.");return passed;
    }
}
