namespace Automation
{
    class Program
    {
        /// <summary>
        /// Main entry point to application.
        /// </summary>
        /// <param name="args">CLI arguments.</param>
        static void Main (string [] args)
        {
            List<string> toRun = new ()
            {
                "",
            };

            foreach (string run in toRun)
            {
                _ = new FC25(run);
            }

            Utility.Utility.ShutdownPC ();                
        }
    }
}