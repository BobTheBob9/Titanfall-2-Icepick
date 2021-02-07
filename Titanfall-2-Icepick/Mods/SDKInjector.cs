﻿using Icepick.Extensions;
using Syringe;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace Icepick.Mods
{
	public class SDKInjector
	{
		[StructLayout( LayoutKind.Sequential, Pack = 8 )]
		struct SDKSettings
		{
			[CustomMarshalAs( CustomUnmanagedType.LPStr )] public string BasePath;
			public bool DeveloperMode;
		};

		private const int OriginInjectionTimeout = 30;
		private const int SteamInjectionTimeout = 60; // Steam needs to launch Origin, so give it longer to load everything

		private const string OriginProcessName = "Origin";
		private const string TitanfallProcessName = "Titanfall2";
		private const string SteamProxyProcessName = "EASteamProxy";
		private const string LaunchViaSteamUrl = "steam://run/1237970";

		public const string SDKDllName = "TTF2SDK.dll";
		private const string SDKDataPath = @"data\";
		private const string InitializeFunction = "InitialiseSDK";

		public delegate void InjectorEventDelegate( string message = null );
		public static event InjectorEventDelegate OnLaunchingProcess;
		public static event InjectorEventDelegate OnInjectingIntoProcess;
		public static event InjectorEventDelegate OnInjectionComplete;
		public static event InjectorEventDelegate OnInjectionException;

		public static async void LaunchAndInject( Launcher launcher, string gamePath = null )
		{
			if( OnLaunchingProcess != null )
			{
				OnLaunchingProcess();
			}


			switch(launcher)
			{
				case Launcher.Origin:
					Process.Start( new ProcessStartInfo( gamePath ) );
					await WatchAndInject( gamePath );
					break;
				case Launcher.Steam:
					Process.Start( LaunchViaSteamUrl );
					await WatchAndInject( TitanfallProcessName, SteamInjectionTimeout );
					break;
				case Launcher.Unpacked:
					try
                    {
						Process newProc = Process.Start("Titanfall2-unpacked.exe", "-multiple");
						await WatchAndInject(newProc.ProcessName, OriginInjectionTimeout, newProc.Id);
					}
					catch (Exception ex)
                    {
						MessageBox.Show(ex.ToString());
                    }
					break;
				default:
					throw new NotImplementedException();
			}
		}

		protected static async Task WatchAndInject( string gamePath, int injectionTimeout = OriginInjectionTimeout, int knownPID = -1 )
		{
			string gameProcessName = System.IO.Path.GetFileNameWithoutExtension( gamePath );
			DateTime startTime = DateTime.Now;

			// HACK: delay this because the launcher is built for late injection normally, can fail if we inject too early
			await Task.Delay(1000);  

			while ( (DateTime.Now - startTime).TotalSeconds < injectionTimeout )
			{
				Process[] ttfProcesses = Process.GetProcessesByName( gameProcessName );
				if( ttfProcesses.Length > 0 )
				{
					Process ttfProcess = ttfProcesses[0];
					if (knownPID != -1)
                    {
						// loop through all processes until we find the one we're trying to inject into
						foreach (Process proc in ttfProcesses)
							if (proc.Id == knownPID)
                            {
								foreach (ProcessModule module in ttfProcess.Modules)
									if (module.ModuleName == "tier0.dll")
                                    {
										InjectSDK(proc);
										return;
									}
							}
                    }
					
					try
					{
						Process potentialOriginProcess = ttfProcess.GetParentProcess();
						if( potentialOriginProcess != null && ( potentialOriginProcess.ProcessName == OriginProcessName || potentialOriginProcess.ProcessName == SteamProxyProcessName ) )
						{
							foreach ( ProcessModule module in ttfProcess.Modules )
							{
								if ( module.ModuleName == "tier0.dll" )
								{
									InjectSDK(ttfProcess);
									return;
								}
							}
						}
					}
					catch ( Win32Exception e )
					{
						if ( OnInjectionException != null )
						{
							OnInjectionException( e.Message + ", Error Code " + e.NativeErrorCode );
						}
					}
					catch ( Exception e )
					{
						if ( OnInjectionException != null )
						{
							OnInjectionException( e.Message );
						}
					}
				}

				await Task.Delay( 1000 );
			}

			// Will only reach here if injection doesn't occur within the timeout period, so log an event and show a popup
			string timeoutError = string.Format( "Timed out after {0} seconds. Could not find Titanfall 2 process.", injectionTimeout );
			if ( OnInjectionException != null )
			{
				OnInjectionException( timeoutError );
			}
			MessageBox.Show( timeoutError, "Injection Failed", MessageBoxButton.OK, MessageBoxImage.Exclamation );
		}
		
		protected static void InjectSDK( Process targetProcess )
		{
			if( OnInjectingIntoProcess != null )
			{
				OnInjectingIntoProcess();
			}

			Injector syringe = new Injector( targetProcess );
			syringe.SetDLLSearchPath( System.IO.Directory.GetCurrentDirectory() );
			syringe.InjectLibrary( SDKDllName );

			SDKSettings settings = new SDKSettings();
			settings.BasePath = AppDomain.CurrentDomain.BaseDirectory + SDKDataPath;
			settings.DeveloperMode = Api.IcepickRegistry.ReadEnableDeveloperMode();
			syringe.CallExport( SDKDllName, InitializeFunction, settings );

			if( OnInjectionComplete != null )
			{
				OnInjectionComplete();
			}
		}

	}
}
