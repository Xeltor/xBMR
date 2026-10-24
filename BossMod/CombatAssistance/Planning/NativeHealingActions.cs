namespace BossMod.CombatAssistance.Planning;

// Effect semantics only. Range, enabled state, gauge/MP/cooldown eligibility,
// configured thresholds and HoT conditions always come from current evidence.
// RSR's target labels include Self/BeAttacked as well as Heal; labels alone
// cannot distinguish healing from a shield, a support link or a friendly buff.
internal static class NativeHealingActions
{
    internal static bool Supported(uint id)
        => (WHM.AID)id is WHM.AID.Cure or WHM.AID.CureII or WHM.AID.Regen or WHM.AID.Benediction
            or WHM.AID.AfflatusSolace or WHM.AID.Tetragrammaton or WHM.AID.Medica or WHM.AID.MedicaII
            or WHM.AID.MedicaIII or WHM.AID.AfflatusRapture
        || (SCH.AID)id is SCH.AID.Physick or SCH.AID.Adloquium or SCH.AID.Succor or SCH.AID.Lustrate
            or SCH.AID.Indomitability or SCH.AID.Concitation or SCH.AID.Manifestation or SCH.AID.Accession
        || (AST.AID)id is AST.AID.Benefic or AST.AID.BeneficII or AST.AID.AspectedBenefic or AST.AID.Helios
            or AST.AID.AspectedHelios or AST.AID.HeliosConjunction or AST.AID.EssentialDignity
            or AST.AID.CelestialIntersection or AST.AID.CelestialOpposition or AST.AID.LadyOfCrowns
        || (SGE.AID)id is SGE.AID.Diagnosis or SGE.AID.EukrasianDiagnosis or SGE.AID.Prognosis
            or SGE.AID.EukrasianPrognosis or SGE.AID.EukrasianPrognosisII or SGE.AID.Druochole
            or SGE.AID.Taurochole or SGE.AID.Ixochole or SGE.AID.Physis or SGE.AID.PhysisII
            or SGE.AID.Holos;
}
