﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using Library;
using System.Net;

//namespace cnslServer
namespace Server
{

    //receive data port = 25002;
    //submit data port = 25003;;
    class Program
    {
        private static Dictionary<int, Client> PlayerQueue;
        private static Dictionary<int, Client> OnlinePlayers;
        private static List<Match> ActiveGames;
        private static List<Match> PendingGames;
        private static Dictionary<Client, Client> PendingInvites;

        private static NetworkStream stream;

        static void Main(string[] args)
        {   
            OnlinePlayers = new Dictionary<int, Client>();
            PlayerQueue = new Dictionary<int, Client>();
            ActiveGames = new List<Match>();

            Task Matchmaking = new Task(HandleMatchmaking);
            Matchmaking.Start();

            Task Listen = new Task(ListenTcp);
            Listen.Start();
            
            Console.ReadLine();
            
        }

        private static async void HandleMatchmaking()
        {
            while (true)
            {
                if (PlayerQueue.Count < 2) continue;

                Console.WriteLine("\nMatchmaking queue:\n");
                foreach(KeyValuePair<int, Client> pair in PlayerQueue)
                {
                    Console.WriteLine(pair.Value.UserID + " - " + pair.Value.Username);
                }
                Console.WriteLine();

                Match match = new Match();
                match.Client1 = PlayerQueue.ElementAt(0).Value;
                match.Client2 = PlayerQueue.ElementAt(1).Value;

                StartMatch(match);
                
                Console.WriteLine("Match has been started between UserID {0} and {1}", match.Client1.UserID, match.Client2.UserID);
                PlayerQueue.Remove(match.Client1.UserID);
                PlayerQueue.Remove(match.Client2.UserID);
            }
        }
        
        private static void StartMatch(Match match)
        {
            try { 
                // Send response to player 1
                Packet packet1 = new Packet();
                packet1.From = "Server";
                packet1.To = match.Client1.UserID.ToString();
                packet1.Type = TcpMessageType.MatchStart;
                var variables = new Dictionary<string, string>();
                variables.Add("UserID", match.Client2.UserID.ToString());
                variables.Add("UserIP", match.Client2.Socket.Client.LocalEndPoint.ToString());
                packet1.Variables = variables;

                SendTcp.SendPacket(packet1, match.Client1.Socket);

                // Send response to player 2
                Packet packet2 = new Packet();
                packet2.From = "Server";
                packet2.To = match.Client1.Socket.Client.LocalEndPoint.ToString();
                packet2.Type = TcpMessageType.MatchStart;
                var variables2 = new Dictionary<string, string>();
                variables2.Add("UserID", match.Client1.UserID.ToString());
                variables2.Add("UserIP", match.Client1.Socket.Client.LocalEndPoint.ToString());
                packet2.Variables = variables2;

                SendTcp.SendPacket(packet2, match.Client2.Socket);

                //Add match to ActiveGames list.
                ActiveGames.Add(match);
            }
            catch
            {
                Console.WriteLine("Unable to connect players. Check if the UserID or Socket are not empty.");
            }
            
        }

        private static void ListenTcp()
        {
            TcpListener server = null;

            try
            {
                // Initialize port & local IP
                Int32 port = 25002;
                IPAddress localAddr = IPAddress.Parse("0.0.0.0");
                
                server = new TcpListener(localAddr, port);

                // Start listening for client requests.
                server.Start();
                Console.WriteLine("TcpListener started. Waiting for a connection... ");

                // Enter the listening loop.
                while (true)
                {
                    // Perform a blocking call to accept requests.
                    // You could also user server.AcceptSocket() here.
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("Incoming connection detected.");
                    

                    Packet packet = SendTcp.ReceivePacket(client);
                    if (packet == null) continue;

                    HandlePacket(packet, new Client {Socket = client });

                    // Send back a response.
                    //if(Response != null)
                    //{
                    //    byte[] msg = System.Text.Encoding.ASCII.GetBytes(Response.ToString());
                    //    stream.Write(msg, 0, msg.Length);
                    //    Console.WriteLine("Sent: {0}", Response.ToString());
                    //}
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
            }
        }

        private static void HandlePacket(Packet packet, Client client)
        {
            if (packet == null) return;
            Packet response = new Packet();
            response.Type = TcpMessageType.Response;

            if (packet.From != "Server") IsClientValid(int.Parse(packet.From));
            if (packet.To != "Server") IsClientValid(int.Parse(packet.To));

            try
            {
                switch (packet.Type)
                {
                    case TcpMessageType.ChatMessage:
                        {
                            int fromUserID = int.Parse(packet.From);
                            int targetUserID = int.Parse(packet.To);
                            string chatmessage = packet.Variables["Chatmessage"];

                            //Check if user is offline
                            if (!IsClientValid(targetUserID))
                            {
                                Console.WriteLine("UserID {0} tried to send a message to offline UserID {1}", fromUserID, targetUserID);
                                SendErrorToClient("Server", client, "Chatmessage failed. User is offline.");
                                return;
                            } 
                            else //if online
                            {
                                //Send Packet to destination
                                SendTcp.SendPacket(new Packet(fromUserID.ToString(), targetUserID.ToString(), TcpMessageType.ChatMessage, new[] {"Chatmessage", chatmessage }), GetClientFromOnlinePlayersByUserID(targetUserID).Socket);

                                //Send response to sender
                                SendSuccessResponse(packet, client);

                                Console.WriteLine("Chatmessage sent from {0} to {1}",packet.From, packet.To);
                            }
                            
                            break;
                        }

                    case TcpMessageType.Command:
                        break;

                    case TcpMessageType.MapData:
                        break;

                    case TcpMessageType.Message:
                        {
                            //int from = int.Parse(packet.From);
                            //int to = int.Parse(packet.To);
                            //string message = packet.Variables["Message"];
                            //string IPdestination = packet.Variables["IPdestination"];
                            break;
                        }

                    case TcpMessageType.None:
                        break;

                    case TcpMessageType.PlayerUpdate:
                        {
                            if (!packet.Variables.ContainsKey("PlayerAction")) break;

                            //Get match
                            Match match = ActiveGames.Where(x => x.Client1 == client || x.Client2 == client).FirstOrDefault();
                            if (match == null) break;

                            Client player = null;
                            Client opponent = null;

                            //Check which Client is Client1 and Client2
                            if (client == match.Client1)
                            {
                                player = match.Client1;
                                opponent = match.Client2;
                            }
                            else
                            {
                                player = match.Client2;
                                opponent = match.Client1;
                            }

                            if (opponent != null) break;

                            //Switch PlayerAction
                            switch (packet.Variables["PlayerAction"])
                            {
                                case "PlayCard":
                                    {
                                        if (!packet.Variables.ContainsKey("CardType")) break;

                                        switch (packet.Variables["CardType"])
                                        {
                                            case "Minion":
                                                {
                                                    //Check requirements of incoming packet
                                                    if (!packet.Variables.ContainsKey("Health")
                                                        || (!packet.Variables.ContainsKey("Attack"))
                                                        || (!packet.Variables.ContainsKey("EnergyCost"))
                                                        || (!packet.Variables.ContainsKey("EffectType")) 
                                                        || (!packet.Variables.ContainsKey("Effect")))
                                                        return;

                                                    //Create packet for opponent
                                                    Packet minionPlayed = new Packet(
                                                        packet.From,
                                                        opponent.UserID.ToString(),
                                                        TcpMessageType.PlayerUpdate,
                                                        new[] {
                                                            "PlayerAction", PlayerAction.PlayCard.ToString(),
                                                            "CardType", CardType.Minion.ToString(),
                                                            "Health", packet.Variables["Health"],
                                                            "Attack", packet.Variables["Attack"],
                                                            "EnergyCost", packet.Variables["EnergyCost"],
                                                            "EffectType", packet.Variables["EffectType"],
                                                            "Effect", packet.Variables["Effect"]
                                                        });

                                                    //Send packet to opponent
                                                    SendTcp.SendPacket(minionPlayed, opponent.Socket);

                                                    SendSuccessResponse(packet, client);
                                                    break;
                                                }
                                            case "Spell":
                                                {
                                                    //Check requirements of incoming packet
                                                    if ( (!packet.Variables.ContainsKey("EnergyCost")) || (!packet.Variables.ContainsKey("Effect")) ) break;

                                                    //Create packet for opponent
                                                    Packet spellPlayed = new Packet(
                                                        packet.From,
                                                        opponent.UserID.ToString(),
                                                        TcpMessageType.PlayerUpdate,
                                                        new[] {
                                                            "PlayerAction", PlayerAction.PlayCard.ToString(),
                                                            "CardType", CardType.Minion.ToString(),
                                                            "EnergyCost", packet.Variables["EnergyCost"],
                                                            "Effect", packet.Variables["Effect"]
                                                        });

                                                    //Send packet to opponent
                                                    SendTcp.SendPacket(spellPlayed, opponent.Socket);

                                                    SendSuccessResponse(packet, client);
                                                    break;
                                                }
                                        }
                                        break;
                                    }
                                case "Attack":
                                    {
                                        //Check requirements for incoming packet
                                        if ((!packet.Variables.ContainsKey("AttackingMinionID"))
                                            || (!packet.Variables.ContainsKey("TargetMinionID")))
                                            break;

                                        //Create packet for opponent
                                        Packet attack = new Packet(
                                                        packet.From,
                                                        opponent.UserID.ToString(),
                                                        TcpMessageType.PlayerUpdate,
                                                        new[] {
                                                            "PlayerAction", PlayerAction.PlayCard.ToString(),
                                                            "CardType", CardType.Minion.ToString(),
                                                            "EnergyCost", packet.Variables["EnergyCost"],
                                                            "Effect", packet.Variables["Effect"]
                                                        });

                                        //Send packet to opponent
                                        SendTcp.SendPacket(attack, opponent.Socket);

                                        SendSuccessResponse(packet, client);
                                        break;
                                    }
                                case "EndTurn":
                                    {   
                                        SendTcp.SendPacket(packet, opponent.Socket);
                                        SendSuccessResponse(packet, client);
                                        break;
                                    }
                            }
                            break;
                        }

                    case TcpMessageType.AddPlayerToQueue:
                        {
                            client.UserID = int.Parse(packet.From);

                            if (!PlayerQueue.ContainsKey(client.UserID))
                            {
                                PlayerQueue.Add(client.UserID, client);
                                Console.WriteLine("UserID {0} has been added to the player queue", client.UserID.ToString());
                                SendSuccessResponse(packet, client);
                            }
                            else if (PlayerQueue.ContainsKey(client.UserID))
                            {
                                Console.WriteLine("UserID {0} tried to add himself to the queue while he's already queued up!");
                                SendSuccessResponse(packet, client);
                            }
                            
                            break;
                        }
                    case TcpMessageType.Login:
                        {
                            int userID = int.Parse(packet.From);
                            string username = packet.Variables["Username"];
                            
                            if (!packet.Variables.ContainsKey("Username") || username != "" || username != string.Empty)
                            {
                                Packet error = new Packet("Server", packet.From, TcpMessageType.Error, new[] { "ErrorMessage", "Please provide a valid username" });
                                SendTcp.SendPacket(error, client.Socket);
                            }

                            Client _client = new Client
                            {
                                UserID = userID,
                                Username = username,
                                Socket = client.Socket
                            };

                           
                            if (!IsClientValid(_client.UserID))
                            {
                                OnlinePlayers.Add(_client.UserID, _client);
                                Console.WriteLine(_client.Username + " logged in");
                                SendSuccessResponse(packet, client);

                                _client.Listen = new Thread(() => ListenToClient(_client));
                                _client.Listen.Start();
                            }
                            else
                            {
                                Console.WriteLine(_client.Username + " tried to log in while it's already logged in. Login aborted.");

                                if (IsClientValid(_client.UserID))
                                {
                                    SendSuccessResponse(packet, client);
                                } 
                                else
                                {
                                    Packet error = new Packet();
                                    packet.From = "Server";
                                    packet.To = _client.UserID.ToString();
                                    packet.Type = TcpMessageType.Error;
                                    Console.WriteLine("{0} tried to log in while its already logged in. Its socket isn't valid anymore", _client.Username);
                                    SendTcp.SendPacket(error, _client.Socket);
                                }
                            }

                            ShowOnlinePlayers();
                            break;
                        }
                    case TcpMessageType.Logout:
                        {
                            int userID = int.Parse(packet.From);

                            if (IsClientValid(userID))
                            {
                                Console.WriteLine("UserID {0} logged out", userID);
                                SendSuccessResponse(packet, client);

                                OnlinePlayers[userID].Socket.Close();
                                OnlinePlayers.Remove(userID);

                                ShowOnlinePlayers();
                            }
                            else
                            {
                                Console.WriteLine("User {0} tried to log out while its not logged in. \n\n", packet.From);
                            }

                            client.Socket.Close();
                            break;
                        }
                    case TcpMessageType.CancelMatchmaking:
                        {
                            if (PlayerQueue.ContainsKey(int.Parse(packet.From)))
                            {
                                PlayerQueue.Remove(int.Parse(packet.From));
                            }

                            SendSuccessResponse(packet, client);
                            break;
                        }
                    case TcpMessageType.SendGameInvite:
                        {
                            int fromUserID = int.Parse(packet.From);
                            int toUserID = int.Parse(packet.To);
                            
                            if (!IsClientValid(toUserID)) break;
                            
                            //If pending game already exists, do nothing
                            if (PendingGames.Where(x => x.Client1 == client).FirstOrDefault() != null)
                            {
                                break;
                            }
                            else
                            {
                                //Create new pending game
                                Match match = new Match()
                                {
                                    Client1 = client,
                                    Client2 = OnlinePlayers[toUserID]
                                };

                                PendingGames.Add(match);

                                //Send packet to client2
                                Communicate(packet);
                                break;
                            }
                        }
                    case TcpMessageType.CancelGameInvite:
                        {
                            var pendinggame = PendingGames.Where(x => x.Client1 == client).FirstOrDefault();
                            if(pendinggame != null)PendingGames.Remove(pendinggame);

                            Communicate(packet);
                            break;
                        }
                    case TcpMessageType.AcceptIncomingGameInvite:
                        {
                            if (IsClientValid(int.Parse(packet.To)))
                            {
                                int senderID = int.Parse(packet.To);
                                Match game = PendingGames.Where(x => x.Client1.UserID == senderID).FirstOrDefault();
                                if (game != null)
                                {
                                    PendingGames.Remove(game);
                                    StartMatch(game);
                                }
                                break;
                            }
                            else
                            {
                                SendErrorToClient("Server", client, "Could not send game invite response. Target user is offline.");
                                break;
                            }
                        }
                    case TcpMessageType.RefuseIncomingGameInvite:
                        {
                            int from = int.Parse(packet.From);
                            int to = int.Parse(packet.To);
                            
                            if (!IsClientValid(to)) break;
                            Client toClient = OnlinePlayers[to];

                            Communicate(packet);

                            //Remove game from PendinGames
                            Match pendinggame = PendingGames.Where(x => x.Client1 == toClient).FirstOrDefault();
                            if (pendinggame != null) PendingGames.Remove(pendinggame);

                            break;
                        }
                    case TcpMessageType.SendFriendRequest:
                        {
                            int targetUserID = int.Parse(packet.To);
                            if (!IsClientValid(targetUserID))
                            {
                                SendErrorToClient("Server", client, "Could not send friend request. Targeted user is offline.");
                                break;
                            }

                            Communicate(packet);
                            SendSuccessResponse(packet, client);
                            break;
                        }
                    case TcpMessageType.CancelFriendRequest:
                        {
                            Communicate(packet);
                            SendSuccessResponse(packet, client);
                            break;
                        }
                    case TcpMessageType.RefuseFriendRequest:
                        {
                            Communicate(packet);
                            SendSuccessResponse(packet, client);
                            break;
                        }
                    case TcpMessageType.AcceptFriendRequest:
                        {
                            Communicate(packet);
                            SendSuccessResponse(packet, client);
                            break;
                        }
                    
                }

                Console.WriteLine("====================================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not handle packet. Please check the packet syntax.\n\n{0}", ex.ToString());
                return;
            }
        }

        private static void ListenToClient(Client client)
        {
            while (client.Socket.Connected)
            {
                try {
                    Packet packet = SendTcp.ReceivePacket(client.Socket);
                    if (packet == null) continue;
                    HandlePacket(packet, client);
                } catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    client.Socket.Close();
                }
            }
        }

        private static void SendSuccessResponse(Packet ReceivedPacket, Client client)
        {
            Packet packet = new Packet("Server", ReceivedPacket.To, TcpMessageType.Response, new[] { "TcpMessageType", ReceivedPacket.Type.ToString() });
            SendTcp.SendPacket(packet, client.Socket);
        }

        private static Client GetClientFromOnlinePlayersByUserID(int UserID)
        {
            if (!IsClientValid(UserID)) return null;
            else return OnlinePlayers[UserID];
        }

        private static bool IsClientValid(int UserID)
        {
            if (!IsClientValid(UserID)) return false;

            Client user = OnlinePlayers[UserID];

            if (user.Socket.Connected)
            {
                return true;
            }
            else
            {   //Remove client
                user.Socket.Close();
                OnlinePlayers.Remove(UserID);
                return false;
            }
            
        }

        private static void SendPlayerList(Dictionary<int, Client> PlayerList)
        {
            Dictionary<string, string> players = new Dictionary<string, string>();

            foreach(var player in OnlinePlayers)
            {
                string usrID = player.Key.ToString();
                string usrName = player.Value.Username;

                players.Add(usrID, usrName);
            }

            Packet packet = new Packet("Server", "Everyone", TcpMessageType.Message, players);
            Broadcast(packet);
        }

        private static void Broadcast(Packet packet)
        { 
            foreach (var player in OnlinePlayers)
            {
                SendTcp.SendPacket(packet, player.Value.Socket);
            }
        }

        private static void ShowOnlinePlayers()
        {
            Console.WriteLine("Online players:\n");
            foreach (KeyValuePair<int, Client> pair in OnlinePlayers)
            {
                Console.WriteLine("{0} - {1}", pair.Value.UserID, pair.Value.Username);
            }
            Console.WriteLine("");
        }

        private static void SendErrorToClient(string from, Client to,  string errormessage)
        {
            Packet packet = new Packet(from, to.UserID.ToString(), TcpMessageType.Error, new[] {"ErrorMessage", errormessage });
            SendTcp.SendPacket(packet, to.Socket);
        }

        private static bool Communicate(Packet packet)
        {
            try
            {
                int targetUserID = int.Parse(packet.To);
                if (!IsClientValid(targetUserID)) return false;

                Client targetClient = GetClientFromOnlinePlayersByUserID(targetUserID);
                SendTcp.SendPacket(packet, targetClient.Socket);
                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine("Error in Server > Program.cs > Communicate function. Error: " + ex);
                return false;
            }
           

        }
        
    }
}
