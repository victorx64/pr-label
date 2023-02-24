using devrating.factory;

public sealed class Reward
{
    private readonly Formula formula;

    public Reward(Formula formula)
    {
        this.formula = formula;
    }

    public uint Value(double rating)
    {
        return (uint)(formula.WinProbabilityOfA(rating, formula.DefaultRating()) * 100d);
    }
}