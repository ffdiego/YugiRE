using Joey.LookAndFeel;

internal static class NativeSwordAngleChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var origin=new CardPoint(500,400);
        var target=new CardPoint(550,100);
        (int Y,int X,NativeSwordAngle.Constant Key) operands=default;
        int Capture(int y,int x,NativeSwordAngle.Constant key)
        {
            operands=(y,x,key);
            return -65; // sentinel, not a native angle fixture
        }
        check(NativeSwordAngle.Preparation(origin,target,true,0,Capture)==0 &&
            operands==(50,300,NativeSwordAngle.Constant.Aimed005DA730),
            "V04.3: local perspective preparation operand order and start");
        check(NativeSwordAngle.Preparation(origin,target,true,64,Capture)==-32 &&
            NativeSwordAngle.Preparation(origin,target,true,128,Capture)==-65,
            "V04.3: preparation signed division truncates toward zero");
        var otherOrigin=new CardPoint(550,100);
        var otherTarget=new CardPoint(500,400);
        check(NativeSwordAngle.Preparation(otherOrigin,otherTarget,false,0,Capture)==256 &&
            operands==(-50,300,NativeSwordAngle.Constant.Opposite005DA728),
            "V04.3: opposite preparation uses distinct constant and base 256");
        check(NativeSwordAngle.Preparation(otherOrigin,otherTarget,false,64,Capture)==224 &&
            NativeSwordAngle.Preparation(otherOrigin,otherTarget,false,128,Capture)==191,
            "V04.3: opposite preparation adds signed interpolated index");
        NativeSwordAngle.Flight(new(517,298),target,Capture);
        check(operands==(33,198,NativeSwordAngle.Constant.Aimed005DA730),
            "V04.3: flight uses current integer center");
        NativeSwordAngle.Impact(origin,target,Capture);
        check(operands==(50,300,NativeSwordAngle.Constant.Aimed005DA730),
            "V04.3: impact uses original vector");
        NativeSwordAngle.Flight(otherOrigin,otherTarget,Capture);
        check(operands==(-50,-300,NativeSwordAngle.Constant.Aimed005DA730),
            "V04.3: mirrored flight preserves signed operands");
    }
}
