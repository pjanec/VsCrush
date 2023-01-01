using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Configuration;
using System.Windows.Forms;
using System.Drawing;

namespace VsCrush
{
    public class App
    {
        NotifyIcon notifyIcon;
        enum State { Invalid, Compiling, Idle };
        State state = State.Idle;
        bool isEnabled = false;

        public App()
        {
            InitializeTrayIcon();
            SetEnabled( true );
        }

        void SetEnabled( bool enabled )
        {
            isEnabled = enabled;
            if( enabled )
            {
                notifyIcon.Icon = VsCrush.Resources.Enabled;
                notifyIcon.Text = "VsCrush ENABLED";
                //notifyIcon.ShowBalloonTip(2000, "VsCrush", "Compiler priority reduction Enabled", ToolTipIcon.Info);
            }
            else
            {
                notifyIcon.Icon = VsCrush.Resources.Disabled;
                notifyIcon.Text = "VsCrush disabled";
                //notifyIcon.ShowBalloonTip(2000, "VsCrush", "Compiler priority reduction Disabled", ToolTipIcon.Info);
            }
        }

        void InitializeTrayIcon()
        {
            var components = new System.ComponentModel.Container();

            notifyIcon = new NotifyIcon(components);
            notifyIcon.Text = "VsCrush";
            //notifyIcon.Icon = VsCrush.Resources.AppIcon;
            
            var menuItems = new List<MenuItem>();
            menuItems.Add( new MenuItem("Exit", new EventHandler( (s,e) => Exit() )) );

            notifyIcon.ContextMenu = new ContextMenu( menuItems.ToArray() );
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += new EventHandler((s, e) => ToggleEnabled());
        }

        public bool shallQuit = false;
        void Exit()
        {
            // We must manually tidy up and remove the icon before we exit.
            // Otherwise it will be left behind until the user mouses over.
            notifyIcon.Visible = false;
            Application.Exit();
            shallQuit = true;
        }

        void ToggleEnabled()
        {
            SetEnabled( !isEnabled );
        }

        const ProcessPriorityClass NewPriority = ProcessPriorityClass.Idle;

        public bool ReducePriority(out bool compilerFound)
        {
            bool reduced = false;
            compilerFound = false;
            try
            {    
                var allProcesses = Process.GetProcesses();
                foreach( var p in allProcesses )
                {
                    if( p.ProcessName == "cl" )
                    {
                        compilerFound = true;
                        //Console.WriteLine(p.ProcessName);
                    
                        if( p.PriorityClass != NewPriority)
                        {
                            p.PriorityClass = NewPriority;
                            if (p.PriorityClass == NewPriority)
                            {
                                Console.WriteLine("pid {0} priority reduced!", p.Id);
                                reduced = true;
                            }
                        }
                    }
                }
            }
            catch(Exception )
            {
            }
            return reduced;
        }

        int TimeWithNoCompiler = 0; // how long we are without a compiler detected
        
        void Sleep(int msec )
        {
            int remains = msec;
            while( remains > 0 )
            {
                Application.DoEvents();
                Thread.Sleep(250);
                remains -= 250;
            }
        }
        
        public void Run()
        {
            State prevState = State.Invalid;

            while(!shallQuit)
            {
                Application.DoEvents();

                if( !isEnabled )
                {
                    Sleep(1000);
                    continue;
                }
                // notify about state change
                if( prevState != state )
                {
                    if( state == State.Idle )
                    {
                        Console.WriteLine("No compilation, waiting...");
                    }
                    if( state == State.Compiling )
                    {
                        Console.WriteLine("Compiler detected, starting to reduce its priority!");
                    }
                    prevState = state;
                }

                if( state == State.Idle )
                {
                    bool compilerFound;
                    bool reduced = ReducePriority(out compilerFound);
                    if (compilerFound)
                    {
                        state = State.Compiling;
                        TimeWithNoCompiler = 0;
                    }
                    else
                    {
                        Sleep(2000); // low rate process scan
                    }
                }

                if( state == State.Compiling )
                {
                    bool compilerFound;
                    bool reduced = ReducePriority(out compilerFound);
                    const int sleepTime = 250;
                    Sleep(sleepTime); // high rate process scan
                    if( !compilerFound )
                    {
                        TimeWithNoCompiler += sleepTime;
                        if( TimeWithNoCompiler > 5000 )
                        {
                            state = State.Idle;
                        }
                    }
                }

            }
        }

        static Mutex singleInstanceMutex;

        public bool AppInstanceAlreadyRunning()
        {
            bool createdNew;

            singleInstanceMutex = new Mutex(true, String.Format("VsCrush"), out createdNew);

            if (!createdNew)
            {
                // myApp is already running...
                return true;
            }
            return false;
        }

    }

    class Program
    {
        
        static void Main(string[] args)
        {
            App app = new App();
            if( app.AppInstanceAlreadyRunning() ) return;
            app.Run();
        }
    }
}
