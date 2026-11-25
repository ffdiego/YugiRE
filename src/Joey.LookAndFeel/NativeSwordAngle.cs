namespace Joey.LookAndFeel;

/// <summary>Operand routing of the x87 calls in 004142C0.
/// Actual constant bytes and __ftol implementation require recipe 32.
/// Inject the conversion rather than asserting Math.PI or rounding semantics.</summary>
public static class NativeSwordAngle
{
    public enum Constant { Aimed005DA730, Opposite005DA728 }

    // FPATAN computes atan2(ST1, ST0). The delegate receives these
    // integer operands in (y,x) order and the actual multiplier selector.
    public delegate int ConvertAngle(int y,int x,Constant multiplier);

    public static int Preparation(CardPoint origin,CardPoint target,
        bool targetOppositePerspective,int counter,ConvertAngle convert)
    {
        ArgumentNullException.ThrowIfNull(convert);
        if(counter is <0 or >128)throw new ArgumentOutOfRangeException(nameof(counter));
        int dx=checked(target.X-origin.X);
        int dy=checked(target.Y-origin.Y);
        int raw=targetOppositePerspective
            ? convert(dx,checked(-dy),Constant.Aimed005DA730)
            : convert(dx,dy,Constant.Opposite005DA728);
        int scaled=checked(raw*counter)/128;
        return targetOppositePerspective?scaled:checked(256+scaled);
    }

    public static int Flight(CardPoint current,CardPoint target,ConvertAngle convert)
    {
        ArgumentNullException.ThrowIfNull(convert);
        return convert(checked(target.X-current.X),checked(current.Y-target.Y),
            Constant.Aimed005DA730);
    }

    public static int Impact(CardPoint origin,CardPoint target,ConvertAngle convert)
        => Flight(origin,target,convert);
}
