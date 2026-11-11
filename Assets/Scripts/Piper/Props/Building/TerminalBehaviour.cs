namespace PipeWorld.Piper
{
    // The terminal in a building's wall. It stands through the wall so it can be used from both sides; each side carries one button
    // per TerminalAction (TerminalButton), and the terminal itself does nothing when clicked.
    public unsafe class TerminalBehaviour : PropBehaviour
    {
        public TerminalBehaviour() : base(PropPrototypeId.Terminal) { }

        public uint BuildingId
        {
            get
            {
                Prop* p = prop;
                return p != null ? p->building : 0;
            }
        }
    }
}
