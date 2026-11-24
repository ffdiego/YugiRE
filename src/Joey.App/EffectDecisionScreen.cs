using Joey.Core;
using Raylib_cs;

static partial class Game
{
    // Presentation preference only. OFF declines optional costs rather than
    // silently accepting them; no native-CPU decision policy is claimed.
    private static EffectPromptMode optionalEffectPrompts = EffectPromptMode.Ask;

    static void ApplyOptionalEffectPromptPolicy()
    {
        while (duel.PendingEffectDecision is { } request)
        {
            var answer = EffectDecisionPolicies.Choose(optionalEffectPrompts, request,
                effect => effect.Kind == EffectDecisionKind.DrawOneOnFlip
                    ? EffectDecisionAnswer.Accept : EffectDecisionAnswer.Decline);
            if (answer is null) return;
            if (!duel.ResolveEffectDecision(request.Id, answer.Value, out message))
                return;
        }
    }

    static void DrawEffectDecisionOverlay()
    {
        if (duel.PendingEffectDecision is not { } request) return;
        bool draw = request.Kind == EffectDecisionKind.DrawOneOnFlip;
        Raylib.DrawRectangle(238, 126, 535, 340, new Color(7, 15, 28, 250));
        Raylib.DrawRectangleLines(238, 126, 535, 340, Gold);
        Label("OPTIONAL EFFECT", 258, 149, 22, Gold);
        Label($"P{request.Owner + 1} - {request.Source.Name}", 258, 192, 17);
        Label(draw ? "Draw one card?" : $"Pay {request.LifePointCost} LP to revive at your next Standby Phase?",
            258, 224, 14);
        if (!draw) Label($"LP: {duel.Life[request.Owner]}", 258, 259, 15);
        if (Button(draw ? "Draw" : "Pay LP", 258, 332, 205, 40))
        {
            if (duel.ResolveEffectDecision(request.Id, EffectDecisionAnswer.Accept, out message))
                ClearDuelSelection();
            return;
        }
        if (Button("Decline", 490, 332, 205, 40))
        {
            if (duel.ResolveEffectDecision(request.Id, EffectDecisionAnswer.Decline, out message))
                ClearDuelSelection();
        }
    }
}
