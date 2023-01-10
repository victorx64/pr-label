public sealed class Reward
{
    private readonly devrating.factory.Formula formula = new devrating.factory.DefaultFormula();

    public double Value(double rating)
    {
        return formula.WinProbabilityOfA(rating, formula.DefaultRating());
    }
}