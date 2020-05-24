using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Windows.Threading; // For Dispatcher
/// Name: Isaiah Plummer
/// SUID: 401056198
// Brushes
using System.Windows.Media;

// observable collections
using System.Collections.ObjectModel;

// INotifyPropertyChanged
using System.ComponentModel;

namespace TicTacToe
{
    class Model : INotifyPropertyChanged
    {
        
        [Serializable]
        struct GameData
        {
            public String Move;
            public bool Start;

            public GameData(String M, bool S)
            {
                Move = M;
                Start = S;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        UdpClient _dataSocket;

        // some data that keeps track of ports and addresses
        private static UInt32 _localPort;
        private static String _localIPAddress;
        private static UInt32 _remotePort;
        private static String _remoteIPAddress;

        private String curMove;
        private bool curStart;

        private Thread _receiveDataThread;
        private Thread _synchWithOtherPlayerThread;


        public ObservableCollection<Tile> TileCollection;
        private static UInt32 _numTiles = 9;
        private char[] _buttonPresses = new char[_numTiles];

        private String _statusText = "";
        public String StatusText

        {
            get { return _statusText; }
            set
            {
                _statusText = value;
                OnPropertyChanged("StatusText");
            }
        }

        private bool _sendEnabled;
        public bool SendEnabled
        {
            get { return _sendEnabled; }
            set
            {
                _sendEnabled = value;
                OnPropertyChanged("SendEnabled");
            }
        }

        private bool _start;
        public bool Starting
        {
            get { return _start; }
            set
            {
                _start = value;
                OnPropertyChanged("Starting");
            }
        }
        private bool _myTurn;
        public bool MyTurn
        {
            get { return _myTurn; }
            set
            {
                _myTurn = value;
                OnPropertyChanged("MyTurn");
            }
        }
        private bool _connected;
        public bool Connected
        {
            get { return _connected; }
            set
            {
                _connected = value;
                OnPropertyChanged("Connected");
            }
        }
        private bool _active;
        public bool Active
        {
            get { return _active; }
            set
            {
                _active = value;
                OnPropertyChanged("Active");
            }
        }
        private char _piece = 'O';
        public char Piece
        {
            get { return _piece; }
            set
            {
                _piece = value;
                OnPropertyChanged("Piece");
            }
        }


        /// <summary>
        /// Model constructor
        /// </summary>
        /// <returns></returns>
        public Model()
        {
            TileCollection = new ObservableCollection<Tile>();
            for (int i = 0; i < _numTiles; i++)
            {
                TileCollection.Add(new Tile()
                {
                    TileBrush = Brushes.Black,
                    TileLabel = "",
                    TileName = i.ToString(),
                    TileBackground = Brushes.LightGray
                });
            }
            SendEnabled = false;
        }
        //Sets local and remote port and IP
        public void SetLocalNetworkSettings(UInt32 port, String ipAddress)
        {
            _localPort = port;
            _localIPAddress = ipAddress;
        }
        public void SetRemoteNetworkSettings(UInt32 port, String ipAddress)
        {
            _remotePort = port;
            _remoteIPAddress = ipAddress;
        }
        public bool InitModel()
        {
            try
            {
                // set up generic UDP socket and bind to local port
                //
                _dataSocket = new UdpClient((int)_localPort);
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
                return false;
            }

            ThreadStart threadFunction;
            threadFunction = new ThreadStart(SynchWithOtherPlayer);
            _synchWithOtherPlayerThread = new Thread(threadFunction);
            StatusText = DateTime.Now + ":" + " Waiting for other UDP peer to join.\n";
            _synchWithOtherPlayerThread.Start();

            return true;
        }
        public void SendMove(String M, bool Starting)
        {
            // data structure used to communicate data with the other player
            GameData gameData;

            // formatter used for serialization of data
            BinaryFormatter formatter = new BinaryFormatter();

            // stream needed for serialization
            MemoryStream stream = new MemoryStream();

            // Byte array needed to send data over a socket
            Byte[] sendBytes;


            // we make sure that the data in the boxes is in the correct format
            try
            {
                Debug.Write("Trying data "+ M + "\n");
                gameData.Move = M;
                gameData.Start = Starting;
                Debug.Write("That worked" + "\n");

            }
            catch (System.Exception)
            {
                // we get here if the format of teh data in the boxes was incorrect. Most likely the boxes we assumed
                // had integers in them had characters as well
                StatusText = StatusText + DateTime.Now + " Data not in correct format! Try again.\n";
                return;
            }

            // serialize the gameData structure to a stream
            formatter.Serialize(stream, gameData);
            Debug.Write("Serialized" + "\n");

            // retrieve a Byte array from the stream
            sendBytes = stream.ToArray();
            Debug.Write("Retrieved byte array" + "\n");
            // send the serialized data
            IPEndPoint remoteHost = new IPEndPoint(IPAddress.Parse(_remoteIPAddress), (int)_remotePort);
            try
            {
                Debug.Write("Sending" + "\n");
                _dataSocket.Send(sendBytes, sendBytes.Length, remoteHost);
                Debug.Write("Sent" + "\n");

            }
            catch (SocketException)
            {
                StatusText = StatusText + DateTime.Now + ":" + " ERROR: Message not sent!\n";
                return;
            }

            StatusText = StatusText + DateTime.Now + ":" + " Move sent successfully.\n";
        }

        /// <summary>
        /// called when the view is closing to ensure we clean up our socket
        /// if we don't, the application may hang on exit
        /// </summary>
        public void Model_Cleanup()
        {
            // important. Close socket or application will not exit correctly.
            if (_dataSocket != null) _dataSocket.Close();
            if (_receiveDataThread != null) _receiveDataThread.Abort();

        }
        private void SynchWithOtherPlayer()
        {

            // set up socket for sending synch byte to UDP peer
            // we can't use the same socket (i.e. _dataSocket) in the same thread context in this manner
            // so we need to set up a separate socket here
            Byte[] data = new Byte[1];
            IPEndPoint endPointSend = new IPEndPoint(IPAddress.Parse(_remoteIPAddress), (int)_remotePort);
            IPEndPoint endPointRecieve = new IPEndPoint(IPAddress.Any, 0);

            UdpClient synchSocket = new UdpClient((int)_localPort + 10);

            // set timeout of receive to 1 second
            _dataSocket.Client.ReceiveTimeout = 1000;

            while (true)
            {
                try
                {
                    synchSocket.Send(data, data.Length, endPointSend);
                    _dataSocket.Receive(ref endPointRecieve);

                    // got something, so break out of loop
                    break;
                }
                catch (SocketException ex)
                {
                    // we get an exception if there was a timeout
                    // if we timed out, we just go back and try again
                    if (ex.ErrorCode == (int)SocketError.TimedOut)
                    {
                        Debug.Write(ex.ToString());
                    }
                    else
                    {
                        // we did not time out, but got a really bad 
                        // error
                        synchSocket.Close();
                        StatusText =  "Socket exception occurred. Unable to sync with other UDP peer.\n";
                        StatusText = StatusText + ex.ToString();
                        Connected = false;
                        return;
                    }
                }
                catch (System.ObjectDisposedException ex)
                {
                    // something bad happened. close the socket and return
                    Console.WriteLine(ex.ToString());
                    synchSocket.Close();
                    StatusText = "Error occurred. Unable to sync with other UDP peer.\n";
                    Connected = false;
                    return;
                }

            }

            // send synch byte
            synchSocket.Send(data, data.Length, endPointSend);

            // close the socket we used to send periodic requests to player 2
            synchSocket.Close();

            // reset the timeout for the dataSocket to infinite
            // _dataSocket will be used to recieve data from other UDP peer
            _dataSocket.Client.ReceiveTimeout = 0;

            // start the thread to listen for data from other UDP peer
            ThreadStart threadFunction = new ThreadStart(ReceiveThreadFunction);
            _receiveDataThread = new Thread(threadFunction);
            _receiveDataThread.Start();


            // got this far, so we received a response from player 2
            StatusText = DateTime.Now + ":" + " Other UDP peer has joined the session.\n";
            Connected = true;
            SendEnabled = true;
        }

        private void ReceiveThreadFunction()
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
            while (true)
            {
                try
                {
                    // wait for data
                    Debug.Write("Waiting for data" + "\n");
                    Byte[] receiveData = _dataSocket.Receive(ref endPoint);
                    Debug.Write("Got it" + "\n");

                    // check to see if this is synchronization data 
                    // ignore it. we should not recieve any sychronization
                    // data here, because synchronization data should have 
                    // been consumed by the SynchWithOtherPlayer thread. but, 
                    // it is possible to get 1 last synchronization byte, which we
                    // want to ignore
                    if (receiveData.Length < 1)
                        continue;
                    Debug.Write("Continuing" + "\n");


                    // process and display data


                    GameData gameData;
                    BinaryFormatter formatter = new BinaryFormatter();
                    MemoryStream stream = new MemoryStream();

                    // deserialize data back into our GameData structure
                    stream = new System.IO.MemoryStream(receiveData);
                    gameData = (GameData)formatter.Deserialize(stream);

                    // update view data through our bound properties
                    Debug.Write("Checking if its starting" + "\n");

                    if (gameData.Start)
                    {
                        Debug.Write("Game Starts" + "\n");

                        StartTheGame();
                    }
                    else
                    {
                        Debug.Write("Recieves Move" + "\n");
                        //This is what breaks it no idea how to call this function from the main thread.
                        UserSelection(gameData.Move, Piece);
                        
                        Debug.Write("Displays Move" + "\n");
                        MyTurn = true;
                        Debug.Write("Its your turn" + "\n");

                    }

                }
                catch (SocketException ex)
                {
                    // got here because either the Receive failed, or more
                    // or more likely the socket was destroyed by 
                    // exiting from the JoystickPositionWindow form
                    Console.WriteLine(ex.ToString());
                    return;
                }
                catch (Exception ex)
                {
                    Console.Write(ex.ToString());
                }

            }
        }

        /// <summary>
        /// processes all buttons. called from view when a button is clicked
        /// </summary>
        /// <param name="buttonSelected"></param>
        /// <returns></returns>
        public bool UserSelection(String buttonSelected,char t)
        {
            Debug.Write("Button selected was " + buttonSelected + "\n");
            
            int index = int.Parse(buttonSelected);
            Debug.Write("Parsed index" + "\n");

            if (_buttonPresses[index] == ' ')
            {
                Debug.Write("If space is blank" + "\n");
                if (t == 'X')
                {
                    Debug.Write("Write X" + "\n");
                    _buttonPresses[index] = 'X';
                    TileCollection[index].TileLabel = "X";
                    TileCollection[index].TileBrush = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
                    TileCollection[index].TileBrush.Freeze();
                    StatusText = "Player X Selected Button " + index.ToString() + "\n";
                    Piece = 'O';
                }
                else if(t == 'O') 
                {
                    Debug.Write("Write O" + "\n");
                    _buttonPresses[index] = 'O';
                    TileCollection[index].TileLabel = "O";
                    TileCollection[index].TileBrush = new SolidColorBrush(Color.FromArgb(255, 0, 0, 255));
                    TileCollection[index].TileBrush.Freeze();
                    StatusText = "Player O Selected Button " + index.ToString() + "\n";
                    Piece = 'X';
                }
                else
                {
                    Debug.Write("Something broke...\n");
                    return false;
                }
            }
            else
            {
                Debug.Write(buttonSelected + " has already been selected\n");
                StatusText = "Selected Button " + index.ToString() + " has already been selected.\n Choose another tile.\n";
                return false;

            }
            if (checkVictory('X'))
            {
                StatusText = "Player " + 'X' + " has won! \n Congrats press play to initiate a new game";
                Active = false;
            }
            if (checkVictory('O'))
            {
                StatusText = "Player " + 'O' + " has won! \n Congrats press play to initiate a new game";
                Active = false;
            }
            else if (checkTie('X') && checkTie('O'))
            {
                StatusText = "It's a tie \n Please press play to initiate a new game";
                Active = false;
            }
            return true;
        }
        public bool checkVictory(char t)
        {
            //Check rows
            if (_buttonPresses[0] == _buttonPresses[1] && _buttonPresses[1] == _buttonPresses[2] && _buttonPresses[2] == t)
            {
                if(t == 'X')
                {
                    TileCollection[0].TileBackground = Brushes.Red;
                    TileCollection[1].TileBackground = Brushes.Red;
                    TileCollection[2].TileBackground = Brushes.Red;

                }
                else
                {
                    TileCollection[0].TileBackground = Brushes.Blue;
                    TileCollection[1].TileBackground = Brushes.Blue;
                    TileCollection[2].TileBackground = Brushes.Blue;
                }

                return true;
            }
            if (_buttonPresses[3] == _buttonPresses[4] && _buttonPresses[4] == _buttonPresses[5] && _buttonPresses[5] == t)
            {
                if (t == 'X')
                {
                    TileCollection[3].TileBackground = Brushes.Red;
                    TileCollection[4].TileBackground = Brushes.Red;
                    TileCollection[5].TileBackground = Brushes.Red;

                }
                else
                {
                    TileCollection[3].TileBackground = Brushes.Blue;
                    TileCollection[4].TileBackground = Brushes.Blue;
                    TileCollection[5].TileBackground = Brushes.Blue;
                }
                return true;

            }
            if (_buttonPresses[6] == _buttonPresses[7] && _buttonPresses[7] == _buttonPresses[8] && _buttonPresses[8] == t)
            {
                if (t == 'X')
                {
                    TileCollection[6].TileBackground = Brushes.Red;
                    TileCollection[7].TileBackground = Brushes.Red;
                    TileCollection[8].TileBackground = Brushes.Red;

                }
                else
                {
                    TileCollection[6].TileBackground = Brushes.Blue;
                    TileCollection[7].TileBackground = Brushes.Blue;
                    TileCollection[8].TileBackground = Brushes.Blue;
                }
                return true;

            }
            //Check columns
            if (_buttonPresses[0] == _buttonPresses[3] && _buttonPresses[3] == _buttonPresses[6] && _buttonPresses[6] == t)
            {
                if (t == 'X')
                {
                    TileCollection[0].TileBackground = Brushes.Red;
                    TileCollection[3].TileBackground = Brushes.Red;
                    TileCollection[6].TileBackground = Brushes.Red;

                }
                else
                {
                    TileCollection[0].TileBackground = Brushes.Blue;
                    TileCollection[3].TileBackground = Brushes.Blue;
                    TileCollection[6].TileBackground = Brushes.Blue;
                }
                return true;
            }
            if (_buttonPresses[1] == _buttonPresses[4] && _buttonPresses[4] == _buttonPresses[7] && _buttonPresses[7] == t)
            {
                if (t == 'X')
                {
                    TileCollection[1].TileBackground = Brushes.Red;
                    TileCollection[4].TileBackground = Brushes.Red;
                    TileCollection[7].TileBackground = Brushes.Red;

                }
                else
                {
                    TileCollection[1].TileBackground = Brushes.Blue;
                    TileCollection[4].TileBackground = Brushes.Blue;
                    TileCollection[7].TileBackground = Brushes.Blue;
                }
                return true;

            }
            if (_buttonPresses[2] == _buttonPresses[5] && _buttonPresses[5] == _buttonPresses[8] && _buttonPresses[8] == t)
            {
                if (t == 'X')
                {
                    TileCollection[2].TileBackground = Brushes.Red;
                    TileCollection[5].TileBackground = Brushes.Red;
                    TileCollection[8].TileBackground = Brushes.Red;

                }
                else
                {
                    TileCollection[2].TileBackground = Brushes.Blue;
                    TileCollection[5].TileBackground = Brushes.Blue;
                    TileCollection[8].TileBackground = Brushes.Blue;
                }
                return true;

            }
            //Check diagonals
            if (_buttonPresses[0] == _buttonPresses[4] && _buttonPresses[4] == _buttonPresses[8] && _buttonPresses[8] == t)
            {
                if (t == 'X')
                {
                    TileCollection[0].TileBackground = Brushes.Red;
                    TileCollection[4].TileBackground = Brushes.Red;
                    TileCollection[8].TileBackground = Brushes.Red;

                }
                else
                {
                    TileCollection[0].TileBackground = Brushes.Blue;
                    TileCollection[4].TileBackground = Brushes.Blue;
                    TileCollection[8].TileBackground = Brushes.Blue;
                }
                return true;
            }
            if (_buttonPresses[2] == _buttonPresses[4] && _buttonPresses[4] == _buttonPresses[6] && _buttonPresses[6] == t)
            {
                if (t == 'X')
                {
                    TileCollection[2].TileBackground = Brushes.Red;
                    TileCollection[4].TileBackground = Brushes.Red;
                    TileCollection[6].TileBackground = Brushes.Red;

                }
                else
                {
                    TileCollection[2].TileBackground = Brushes.Blue;
                    TileCollection[4].TileBackground = Brushes.Blue;
                    TileCollection[6].TileBackground = Brushes.Blue;
                }
                return true;

            }
            return false;

        }
        public bool checkTie(char t)
        {
            if ((_buttonPresses[0] == ' ' || _buttonPresses[0] == t) && (_buttonPresses[1] == ' '|| _buttonPresses[1] == t) && (_buttonPresses[2] == t || _buttonPresses[2] == ' '))
            {
                return false;
            }

            if ((_buttonPresses[3] == ' ' || _buttonPresses[3] == t) && (_buttonPresses[4] == ' ' || _buttonPresses[4] == t) && (_buttonPresses[5] == t || _buttonPresses[5] == ' '))
            {
                return false;
            }

            if ((_buttonPresses[6] == ' ' || _buttonPresses[6] == t) && (_buttonPresses[7] == ' ' || _buttonPresses[7] == t) && (_buttonPresses[8] == t || _buttonPresses[8] == ' '))
            {
                return false;
            }
            //Check Columns
            if ((_buttonPresses[0] == ' ' || _buttonPresses[0] == t) && (_buttonPresses[3] == ' ' || _buttonPresses[3] == t) && (_buttonPresses[6] == t || _buttonPresses[6] == ' '))
            {
                return false;
            }
            if ((_buttonPresses[1] == ' ' || _buttonPresses[1] == t) && (_buttonPresses[4] == ' ' || _buttonPresses[4] == t) && (_buttonPresses[7] == t || _buttonPresses[7] == ' '))
            {
                return false;
            }
            if ((_buttonPresses[2] == ' ' || _buttonPresses[2] == t) && (_buttonPresses[5] == ' ' || _buttonPresses[5] == t) && (_buttonPresses[8] == t || _buttonPresses[8] == ' '))
            {
                return false;
            }
            //Check diagonals
            if ((_buttonPresses[0] == ' ' || _buttonPresses[0] == t) && (_buttonPresses[4] == ' ' || _buttonPresses[4] == t) && (_buttonPresses[8] == t || _buttonPresses[8] == ' '))
            {
                return false;
            }
            if ((_buttonPresses[2] == ' ' || _buttonPresses[2] == t) && (_buttonPresses[4] == ' ' || _buttonPresses[4] == t) && (_buttonPresses[6] == t || _buttonPresses[6] == ' '))
            {
                return false;
            }
            return true;
        }
       
        /// <summary>
        /// resets all buttons back to their starting point
        /// </summary>
        /// <param name></param>
        /// <returns></returns>
        public void Play()
        {
            Active = true;
            for (int x = 0; x < _numTiles; x++)
            {
                TileCollection[x].TileBrush = Brushes.Black;
                TileCollection[x].TileLabel = "";
                TileCollection[x].TileBackground = Brushes.LightGray;
                _buttonPresses[x] = ' ';
            }

            StatusText = "It is X's turn";
            SendMove(null,true);
        }
        public void StartTheGame()
        {
            Active = true;
            MyTurn = true;
            for (int x = 0; x < _numTiles; x++)
            {
                TileCollection[x].TileBrush = Brushes.Black;
                TileCollection[x].TileLabel = "";
                TileCollection[x].TileBackground = Brushes.LightGray;
                _buttonPresses[x] = ' ';
            }
            Piece = 'X';
            StatusText = "It is your turn";
        }
    }
}
