using OpenCvSharp;

namespace TheTowerFarmer
{
    internal class GameStateDetector(Vision vision)
    {
        private readonly Dictionary<GameStates, string[]> templates = new()
        {
            { GameStates.MainMenu, [Templates.MainMenu.BattleStart] },
            { GameStates.Defeat, [Templates.Defeat.Retry] },
            { GameStates.InBattle, [Templates.Battle.SuperOff, Templates.Battle.EcoOff, Templates.Battle.DefenseOff] },
        };

        public GameStates DetectGameState(Mat frame)
        {
            foreach (var (state, templatePaths) in templates)
            {
                foreach (var templatePath in templatePaths)
                {
                    var match = vision.FindTemplate(frame, templatePath);

                    if (match != null)
                        return state;
                }
            }

            return GameStates.Unknown;
        }
    }
}
