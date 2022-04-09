namespace udemy
{
    public interface IPredicateEvaluator
    {
        bool? evalute(string predicate, string[] parameters);
    }
}
