using Joey.LookAndFeel;

internal static class NativeHelpProducerChecks
{
    public static void Run(Action<bool,string> check)
    {
        var mailbox=new NativeHelpMailbox();
        check(mailbox.Consume()==-1,"V29: mailbox empty sentinel");
        mailbox.Request(7);
        mailbox.Request(0);
        check(mailbox.Consume()==0&&mailbox.Consume()==-1,
            "V29: last write wins, zero is a valid request");
        mailbox.Request(4);
        int? missing=NativeHelpProducers.Position(true,false,true,true,true);
        if(missing is int key)mailbox.Request(key);
        check(mailbox.Consume()==4,"V29: no-write branch preserves prior request");
        check(NativeHelpProducers.Position(false,true,true,true,true) is null&&
            NativeHelpProducers.Position(true,true,true,true,true)==3&&
            NativeHelpProducers.Position(true,true,false,true,true)==0x12&&
            NativeHelpProducers.Position(true,true,false,false,true)==0x10&&
            NativeHelpProducers.Position(true,true,false,false,false) is null,
            "V29: position predicates and precedence");
        check(NativeHelpProducers.BattleProbe(false)==7&&NativeHelpProducers.BattleProbe(true)==8&&
            NativeHelpProducers.HoverZone(0)==9&&NativeHelpProducers.HoverZone(11)==9&&
            NativeHelpProducers.HoverZone(12) is null&&NativeHelpProducers.HoverZone(-1) is null,
            "V29: raw attack and hover keys");
        int? Summon(int level,int tribute=0,bool empty=true,bool after=true,
            bool normal=true,bool prohibited=false,bool cost=true,bool early=false,bool local=true)=>
            NativeHelpProducers.NormalSummonProbe(local,true,prohibited,cost,early,
                normal,level,tribute,empty,after);
        check(Summon(4,normal:false)==0x11&&Summon(4,empty:false,normal:false)==0xb&&
            Summon(5,normal:false)==0&&Summon(5,1,after:false,normal:false)==0xb&&
            Summon(6,1,normal:false)==0x11&&Summon(7,1)==0&&Summon(7,2) is null,
            "V29: tribute/space can overwrite earlier normal summon restriction");
        check(Summon(4,prohibited:true,cost:false)==0x10&&
            Summon(4,cost:false,early:true)==0x17&&Summon(4,early:true) is null&&
            Summon(4,prohibited:true,local:false) is null,
            "V29: earlier summon exits preserve order and side gate");
        check(NativeHelpProducers.IsLocalActivation(0,0)&&
            NativeHelpProducers.IsLocalActivation(0x4001,0)&&
            !NativeHelpProducers.IsLocalActivation(0x4000,0)&&
            NativeHelpProducers.IsLocalActivation(1,1),
            "V29: descriptor XOR activation side");
        check(NativeHelpProducers.ActivationCost(true,500,500)==0x17&&
            NativeHelpProducers.ActivationCost(true,500,501) is null&&
            NativeHelpProducers.ActivationCost(false,500,500) is null,
            "V29: LP must be strictly above cost");
    }
}
