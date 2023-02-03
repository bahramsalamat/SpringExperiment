using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
public class GameManager : MonoBehaviour
{
    public Ghost[] ghosts;
    public Pacman pacman;
    public Transform pellets;
   
    StringBuilder output = new StringBuilder();
    public string ParticipantID;


    public Text gameOverText;
    public Text scoreText;
    public Text livesText;
    public int ghostMultiplier { get; private set; } = 1;
    public int score { get; private set; }
    public int lives { get; private set; }

    [HideInInspector] public bool isTxStarted = false;

    [SerializeField] string IP = "127.0.0.1"; // local host
    [SerializeField] int rxPort = 8000; // port to receive data from Python on
    [SerializeField] int txPort = 8001; // port to send data to Python on

    // Create necessary UdpClient objects
    UdpClient client;
    IPEndPoint remoteEndPoint;
    Thread receiveThread; // Receiving Thread

    public void SendData(string message) // Use to send data to Python
    {
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            client.Send(data, data.Length, remoteEndPoint);
        }
        catch (Exception err)
        {
            print(err.ToString());
        }
    }

    public void udpAwake()
    {
        // Create remote endpoint (to Matlab) 
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(IP), txPort);

        // Create local client
        client = new UdpClient(rxPort);

        // local endpoint define (where messages are received)
        // Create a new thread for reception of incoming messages
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

        // Initialize (seen in comments window)
        print("UDP Comms Initialised");


    }

    // Receive data, update packets received
    private void ReceiveData()
    {
        ParticipantID = DateTime.Now.ToString("yyyyMMddHHmmss");
        while (true)
        {
            
            try
            {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref anyIP);
                string text = Encoding.UTF8.GetString(data);
                print(">> " + text);
                float speed = float.Parse(text);

                //print(">> " + speed);
                string Lives = Convert.ToString(score.ToString());
                string Score = Convert.ToString(lives.ToString());
                string Speed = Convert.ToString(speed.ToString());

                // construct a UDP string with the above signals
                // The prefix "E" lets IMOTIONS know that this is a line graph type of input.
                string UDPstring = "E;1;GenericInput;1;0.0;;;GenericInput;" + Score + ";" + Lives + ";" + Speed + "\r\n";
                SendUDPPacket("127.0.0.1", 8089, UDPstring, 1);
                output.AppendLine(string.Join(",", new String[] { DateTime.Now.ToString(), lives.ToString(), speed.ToString(), score.ToString() }));
               // print(">> " + output);
                if (lives < 3)
                {
                    ResetSpeed(0.5f + speed);
                }

                if (lives > 2 && lives < 5)
                {
                    ResetSpeed(1.5f- speed);
                }
                // ProcessInput(txtstring);
            }
            catch (Exception err)
            {
                //print(err.ToString());
            }
        }
    }
    static void SendUDPPacket(string hostNameOrAddress, int destinationPort, string data, int count)
    {
        // Validate the destination port number
        if (destinationPort < 1 || destinationPort > 65535)
            throw new ArgumentOutOfRangeException("destinationPort", "Parameter destinationPort must be between 1 and 65,535.");

        // Resolve the host name to an IP Address
        IPAddress[] ipAddresses = Dns.GetHostAddresses(hostNameOrAddress);
        if (ipAddresses.Length == 0)
            throw new ArgumentException("Host name or address could not be resolved.", "hostNameOrAddress");

        // Use the first IP Address in the list
        IPAddress destination = ipAddresses[0];
        IPEndPoint endPoint = new IPEndPoint(destination, destinationPort);
        byte[] buffer = Encoding.ASCII.GetBytes(data);

        // Send the packets
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        for (int i = 0; i < count; i++)
            socket.SendTo(buffer, endPoint);
        socket.Close();
    }
    private void ProcessInput(string input)
    {
        // PROCESS INPUT RECEIVED STRING HERE

        if (!isTxStarted) // First data arrived so tx started
        {
            isTxStarted = true;
        }
    }

    //Prevent crashes - close clients and threads properly!
    void OnudpDisable()
    {
        if (receiveThread != null)
            receiveThread.Abort();

        client.Close();
    }

    private void Start()
    {
     NewGame();
     udpAwake();


    }

    private void Update()
    {
        using (StreamWriter writetext = new StreamWriter(ParticipantID+ ".txt"))
        {
            writetext.WriteLine(output);
        }
        if (lives <= 0 && Input.anyKeyDown) {
            output = new StringBuilder();
            NewGame();
        }
    }

    private void NewGame()
    {
        
        SetScore(0);
        SetLives(6);
        NewRound();
        
    }

    private void NewRound()
    {
        gameOverText.enabled = false;

        foreach (Transform pellet in pellets) {
            pellet.gameObject.SetActive(true);
        }

        ResetState();
    }

    private void ResetState()
    {
        for (int i = 0; i < ghosts.Length; i++) {
            ghosts[i].ResetState();
            
        }

        pacman.ResetState();
    }
    private void ResetSpeed(float speedvalue)
    {
        
        for (int i = 0; i < ghosts.Length; i++)
        {
            ghosts[i].ResetSpeed(speedvalue);



        }

    }



    private void GameOver()
    {
        
        gameOverText.enabled = true;
       

        for (int i = 0; i < ghosts.Length; i++) {
            ghosts[i].gameObject.SetActive(false);
        }

        pacman.gameObject.SetActive(false);
        Application.Quit();

    }

    private void SetLives(int lives)
    {
        this.lives = lives;
        livesText.text = "x" + lives.ToString();
    }

    private void SetScore(int score)
    {
        this.score = score;
        scoreText.text = score.ToString().PadLeft(2, '0');
    }

    public void PacmanEaten()
    {
        pacman.DeathSequence();

        SetLives(lives - 1);

        if (lives > 0) {
            Invoke(nameof(ResetState), 3f);
        } else {
            GameOver();
        }


    }

    public void GhostEaten(Ghost ghost)
    {
        int points = ghost.points * ghostMultiplier;
        SetScore(score + points);

        ghostMultiplier++;
    }

    public void PelletEaten(Pellet pellet)
    {
        
        pellet.gameObject.SetActive(false);
        

        SetScore(score + pellet.points);

        if (!HasRemainingPellets())
        {
            pacman.gameObject.SetActive(false);
            Invoke(nameof(NewRound), 3f);
        }
    }

    public void PowerPelletEaten(PowerPellet pellet)
    {
        for (int i = 0; i < ghosts.Length; i++) {
            ghosts[i].frightened.Enable(pellet.duration);
            
        }

        PelletEaten(pellet);
        CancelInvoke(nameof(ResetGhostMultiplier));
        Invoke(nameof(ResetGhostMultiplier), pellet.duration);
    }

    private bool HasRemainingPellets()
    {
        foreach (Transform pellet in pellets)
        {
            if (pellet.gameObject.activeSelf) {
                return true;
            }
        }

        return false;
    }

    private void ResetGhostMultiplier()
    {
        ghostMultiplier = 1;
    }

}
