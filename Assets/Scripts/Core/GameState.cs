namespace FormulaSim.Core
{
    public enum GameState
    {
        MainMenu,
        Loading,
        RaceWeekend,
        Qualifying,
        Formation,
        Racing,
        Paused,
        PitStop,
        SafetyCar,
        Results,
        Career,
        Championship,
        LiveryEditor,
    }

    public enum RaceFlag
    {
        Green,
        Yellow,
        SafetyCar,
        VirtualSafetyCar,
        Red,
        Chequered,
    }
}
