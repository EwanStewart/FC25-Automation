using Automation.Setup;
using System.Linq.Expressions;

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
            try
            {
                FC25 WebApplication = new();
            }
            catch (Exception)
            {

            }
            finally
            {
                Utility.Utility.ShutdownPC ();
            }                
        }
    }
}