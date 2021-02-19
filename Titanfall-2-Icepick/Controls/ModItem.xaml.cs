using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;

namespace Icepick.Controls
{
	/// <summary>
	/// Interaction logic for ModItem.xaml
	/// </summary>
	public partial class ModItem : UserControl
	{
		public enum StatusIconType
		{
			Ok,
			Warning,
			Error,
			Update
		}

		public static string GetIcon( StatusIconType icon )
		{
			switch ( icon )
			{
				default:
				case StatusIconType.Warning:
					return "error.png";
				case StatusIconType.Ok:
					return "accept.png";
				case StatusIconType.Error:
					return "exclamation.png";
				case StatusIconType.Update:
					return "connect.png";
			}
		}

		public ModItem()
		{
			InitializeComponent();
		}

		public ModItem( Mods.TitanfallMod mod )
		{
			InitializeComponent();
			Mod = mod;
			Mod.OnStatusUpdated += Mod_OnStatusUpdated;
            MouseDoubleClick += OnDoubleClick;

			ModName = mod.Definition?.Name;
			ModDescription = mod.Definition?.Description;
			if ( !string.IsNullOrEmpty( mod.ImagePath ) )
			{
				ModImage = System.IO.Path.Combine( Environment.CurrentDirectory, mod.ImagePath );
			}

			RefreshNameAndImageForDisabledState();

			Mod_OnStatusUpdated();
		}

        public Mods.TitanfallMod Mod { get; set; }

		public string ModName
		{
			set
			{
				ModNameLabel.Content = string.IsNullOrWhiteSpace(value) ? "Warning: Unnamed Mod " + System.IO.Path.GetFileName( Mod.Directory ) : value;
			}
			get
			{
				return (string) ModNameLabel.Content;
			}
		}

		public string ModDescription
		{
			set
			{
				ModDescriptionLabel.Content = string.IsNullOrWhiteSpace( value ) ? "Warning: Missing description." : value;
			}
			get
			{
				return (string) ModDescriptionLabel.Content;
			}
		}

		public string ModImage
		{
			set
			{
				ModDisplayImage.Source = new BitmapImage( new Uri( value ) );
			}
		}

		public ImageSource ModImageSource
		{
			get
			{
				return ModDisplayImage.Source;
			}
		}

		public StatusIconType Icon
		{
			set
			{
				BitmapImage logo = new BitmapImage();
				logo.BeginInit();
				logo.UriSource = new Uri( "pack://application:,,,/Titanfall-2-Icepick;component/Images/" + GetIcon( value ) );
				logo.EndInit();
				ModStatusImage.Source = logo;
			}
		}

		private void ShowInExplorer_Click( object sender, RoutedEventArgs e )
		{
			string path = System.IO.Path.Combine( Environment.CurrentDirectory, Mod.Directory );
			System.Diagnostics.Process.Start( path );
		}

		private void ShowOnSite_Click( object sender, RoutedEventArgs e )
		{
			Mod.OpenDownloadPage();
		}

		private void CheckForUpdates_Click( object sender, RoutedEventArgs e )
		{
			Mod.CheckForUpdates();
		}

		private void ViewDetails_Click( object sender, RoutedEventArgs e )
		{
			ModDetailsWindow details = new ModDetailsWindow( this );
			details.Show();
		}

		private void PackageMod_Click( object sender, RoutedEventArgs e )
		{
			string errorMessage = Mods.ModDatabase.PackageMod( Mod.Directory );
			if ( errorMessage == null )
			{
				Mods.ModDatabase.ShowModsFolder();
			}
			else
			{
				MessageBox.Show( $"Could not package mod.\n{errorMessage}", "Package Error", MessageBoxButton.OK, MessageBoxImage.Exclamation );
			}
		}

		private void Mod_OnStatusUpdated()
		{
			UpdateTooltipAndStatus();
		}

		private void UpdateTooltipAndStatus()
		{
			if ( Mod.RequiresUpdate )
			{
				Icon = StatusIconType.Update;
				TooltipHeader.Text = "Update Available";
				TooltipText.Text = "This mod has an update available! Download it from Titanfall Mods via the context menu.";
				return;
			}

			var ErrorsList = Mod.GetErrors();
			var WarningsList = Mod.GetWarnings();

			if ( ErrorsList.Count > 0 || WarningsList.Count > 0 )
			{
				Icon = ErrorsList.Count > 0 ? StatusIconType.Error : StatusIconType.Warning;
				TooltipHeader.Text = "Action Required";
				TooltipText.Text = "";
				foreach ( string error in Mod.GetErrors() )
				{
					TooltipText.Text += TooltipText.Text == "" ? "" : "\n";
					TooltipText.Text += error;
				}
				foreach ( string warning in Mod.GetWarnings() )
				{
					TooltipText.Text += TooltipText.Text == "" ? "" : "\n";
					TooltipText.Text += warning;
				}
				return;
			}

			Icon = StatusIconType.Ok;
			TooltipHeader.Text = "All good";
			TooltipText.Text = "This mod is up to date!";
		}

		private void OnDoubleClick(object sender, MouseButtonEventArgs e)
		{
			// honestly this is all really hacky but it's good enough and i wanted to implement this without touching ttf2sdk, be sure to refactor when writing new launcher
			try
            {
				string jsonPath = System.IO.Path.Combine( Mod.Directory, "mod.json" );

				string jsonContent = File.ReadAllText( jsonPath );
				if ( Mod.Enabled )
					jsonContent = "disabled" + jsonContent; // prevents it from being a valid json file so ttf2sdk won't load it
				else
					jsonContent = jsonContent.Substring( "disabled".Length ); // make it valid again

				File.WriteAllText(jsonPath, jsonContent);

				Mod.Enabled = !Mod.Enabled;
				RefreshNameAndImageForDisabledState();
			}
			catch ( IOException ex )
            {
				MessageBox.Show( $"Encountered an exception when enabling or disabling a mod: {ex}\nThe mod's mod.json file may be unable to be written to!" );
            }
		}

		private void RefreshNameAndImageForDisabledState()
		{
			if ( string.IsNullOrEmpty(Mod.ImagePath) )
				ModImage = "pack://application:,,,/Titanfall-2-Icepick;component/Images/icepick-logo.png";
			else
				ModImage = Mod.ImagePath;

			if ( Mod.Enabled )
				ModName = ModName.Replace("(Disabled) ", "");
			else
            {
				WriteableBitmap iconBitmap = new WriteableBitmap( (BitmapImage)ModDisplayImage.Source );

				// convert to grayscale
				unsafe
                {
					iconBitmap.Lock();

					// https://dzone.com/articles/how-convert-image-gray-scale
					byte* pBuff = (byte*)iconBitmap.BackBuffer.ToPointer();
					for (int y = 0; y < iconBitmap.PixelHeight; y++)
                    {
						byte* row = pBuff + (y * iconBitmap.BackBufferStride);
						for (int x = 0; x < iconBitmap.PixelWidth; x++)
                        {
							byte grayscale = (byte)((row[x * 4 + 1] + row[x * 4 + 2] + row[x * 4 + 3]) / 3);
							row[x * 4] = grayscale;
							row[x * 4 + 1] = grayscale;
							row[x * 4 + 2] = grayscale;
							row[x * 4 + 3] = grayscale;
						}
					}

					iconBitmap.AddDirtyRect(new Int32Rect(0, 0, iconBitmap.PixelWidth, iconBitmap.PixelHeight));
					iconBitmap.Unlock();
				}

				ModDisplayImage.Source = iconBitmap;
				ModName = "(Disabled) " + ModName;
			}
		}
	}
}
