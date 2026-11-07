namespace PipeWorld.Piper
{
    // A building prop with nothing to do but be seen: its presence is what makes the building's inventory reachable.
    public class CargoTerminalBehaviour : PropBehaviour
    {
        public CargoTerminalBehaviour() : base(PropPrototypeId.CargoTerminal) { }
    }
}
