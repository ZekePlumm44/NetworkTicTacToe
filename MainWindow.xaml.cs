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
using SocketSetup;


namespace TicTacToe
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// Name: Isaiah Plummer
    /// SUID: 401056198
    public partial class MainWindow : Window
    {
        private readonly Model _model;



        public MainWindow()
        {
            InitializeComponent();

            // make it so the user cannot resize the window
            this.ResizeMode = ResizeMode.NoResize;

            // create an instance of our Model
            _model = new Model();
            this.DataContext = _model;

            // associate ItemControl with collection. this collection
            // contains the tiles we placed in the ItemsControl
            // the data in the Tile Colleciton will be bound to 
            // each of the UI elements on the display
            MyItemsControl.ItemsSource = _model.TileCollection;

            //Welcomes people to tic tac toe
            _model.StatusText = "Welcome To Tic-Tac-Toe\n Press connect to link then press start";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // one of the buttons in our collection. need to figure out
            // which one. Since we know the button is part of a collection, we 
            // have a special way that we need to get at its
            if (_model.Active && _model.MyTurn && _model.Connected)
            {
                var selectedButton = e.OriginalSource as FrameworkElement;
                if (selectedButton != null)
                {
                    // get the currently selected item in the collection
                    // which we know to be a Tile object
                    // Tile has a TileName (refer to Tile.cs)
                    var currentTile = selectedButton.DataContext as Tile;
                    if(_model.UserSelection(currentTile.TileName, _model.Piece))
                    {
                        _model.SendMove(currentTile.TileName, false);
                        _model.MyTurn = false;
                    }
                    
                    
                }

            }
        }

        private void Play_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!(_model.Active) && _model.Connected)
            {
                _model.Active = true;
                _model.Piece = 'O';
                _model.MyTurn = false;
                _model.Play();
            }
        }
        

        private void SocketSetup_Click(object sender, RoutedEventArgs e)
        {
            SocketSetupWindow socketSetupWindow = new SocketSetupWindow();
            socketSetupWindow.ShowDialog();
            this.Title = this.Title + " " + socketSetupWindow.SocketData.LocalIPString + "@" + socketSetupWindow.SocketData.LocalPort.ToString();
            _model.SetLocalNetworkSettings(socketSetupWindow.SocketData.LocalPort, socketSetupWindow.SocketData.LocalIPString);
            _model.SetRemoteNetworkSettings(socketSetupWindow.SocketData.RemotePort, socketSetupWindow.SocketData.RemoteIPString);
            _model.InitModel();
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _model.Model_Cleanup();
        }
    }
}
