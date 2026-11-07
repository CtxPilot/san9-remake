using San9.Core.Simulation;

namespace San9.Application.Presentation;

public enum PersonnelAdviceGrade { None = 0, Unfavorable = 1, Favorable = 2, Special = 3 }

internal static class PersonnelAdviceGrades
{
    internal static PersonnelAdviceGrade From(PersonnelAdvicePreview preview) => preview.Advice.Rejection != CommandRejection.None
        ? PersonnelAdviceGrade.None : preview.SearchAdvice is not null ? PersonnelAdviceGrade.Special : preview.Advice.Opinion switch
        {
            AdviserOpinion.Favorable => PersonnelAdviceGrade.Favorable,
            AdviserOpinion.Unfavorable => PersonnelAdviceGrade.Unfavorable,
            _ => PersonnelAdviceGrade.None
        };

    internal static PersonnelAdviceGrade Summarize(IEnumerable<PersonnelAdvicePreview> previews)
    {
        bool unfavorable = false, unknown = false, favorable = false;
        foreach (var preview in previews)
        {
            if (preview.Advice.Rejection != CommandRejection.None) continue;
            switch (From(preview))
            {
                case PersonnelAdviceGrade.Special: return PersonnelAdviceGrade.Special;
                case PersonnelAdviceGrade.Favorable: favorable = true; break;
                case PersonnelAdviceGrade.Unfavorable: unfavorable = true; break;
                default: unknown = true; break;
            }
        }
        return favorable ? PersonnelAdviceGrade.Favorable : unfavorable && !unknown ? PersonnelAdviceGrade.Unfavorable : PersonnelAdviceGrade.None;
    }

    internal static string Symbol(PersonnelAdviceGrade grade) => grade switch
    {
        PersonnelAdviceGrade.Special => "◎",
        PersonnelAdviceGrade.Favorable => "○",
        PersonnelAdviceGrade.Unfavorable => "×",
        _ => ""
    };
}
