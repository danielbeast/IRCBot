using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Text.RegularExpressions;

namespace IRCBot
{
    internal struct IRCConfig
    {
        public bool joined;
        public string server;
        public int port;
        public string nick;
        public string name;
        public string channel;

    }

    internal class IRCBot : IDisposable
    {
        private TcpClient IRCConnection = null;
        private IRCConfig config;
        private NetworkStream ns = null;
        private StreamReader sr = null;
        private StreamWriter sw = null;

        public IRCBot(IRCConfig config)
        {
            this.config = config;
        }

        public void Connect()
        {
            try
            {
                IRCConnection = new TcpClient(config.server, config.port);
            }
            catch
            {
                Console.WriteLine("Connection Error");
                throw;
            }

            try
            {
                ns = IRCConnection.GetStream();
                sr = new StreamReader(ns);
                sw = new StreamWriter(ns);
                sendData("USER", config.nick + " 0 * " + config.name);
                sendData("NICK", config.nick);
            }
            catch
            {
                Console.WriteLine("Communication error");
                throw;
            }
        }

        public void sendData(string cmd, string param)
        {
            if (param == null)
            {
                sw.WriteLine(cmd);
                sw.Flush();
                Console.WriteLine(cmd);
            }
            else
            {
                sw.WriteLine(cmd + " " + param);
                sw.Flush();
                Console.WriteLine(cmd + " " + param);
            }
        }

        public void IRCWork()
        {
            string[] ex;
            string data;
            bool shouldRun = true;
            while (shouldRun)
            {
                data = sr.ReadLine();
                Console.WriteLine(data); //Used for debugging
                char[] charSeparator = new char[] { ' ' };
                ex = data.Split(charSeparator, 5); //Split the data into 5 parts
                if (!config.joined) //if we are not yet in the assigned channel
                {
                    if (ex[1] == "MODE") //Normally one of the last things to be sent (usually follows motd)
                    {
                        sendData("JOIN", config.channel); //join assigned channel
                        config.joined = true;
                    }
                }

                if (ex[0] == "PING")  //respond to pings
                {
                    sendData("PONG", ex[1]);
                }


                if (ex.Length > 4) //is the command received long enough to be a bot command?
                {
                    string command = ex[3]; //grab the command sent
                    string output = "";

                    switch (command)
                    {
                        case ":!join":
                            sendData("JOIN", ex[4]);
                            //if the command is !join send the "JOIN" command to the server with the parameters set by the user
                            break;
                        case ":!say":
                            sendData("PRIVMSG", ex[2] + " :" + ex[4]);
                            //if the command is !say, send a message to the chan (ex[2]) followed by the actual message (ex[4]).
                            break;
                        case ":!blame":
                            sendData("PRIVMSG", ex[2] + " :" + "It was "+ex[4]+"'s fault!");
                            break;
                        case ":!weather":
                            Regex regex = new Regex(@"[^\d]");
                            string zip_code = regex.Replace(ex[4].ToUpper(), String.Empty);

                            if (zip_code.Length == 5)
                            {
                                // Create a new XmlDocument  
                                XmlDocument doc = new XmlDocument();

                                // Load data  
                                doc.Load("http://xml.weather.yahoo.com/forecastrss?p=" + zip_code);

                                // Set up namespace manager for XPath  
                                XmlNamespaceManager ns = new XmlNamespaceManager(doc.NameTable);
                                ns.AddNamespace("yweather", "http://xml.weather.yahoo.com/ns/rss/1.0");

                                // Get current conditions with XPath  
                                XmlNodeList nodes = doc.SelectNodes("/rss/channel/item/yweather:condition", ns);

                                // Get location with XPath
                                XmlNodeList nodes2 = doc.SelectNodes("/rss/channel/yweather:location", ns);
                                // You can also get elements based on their tag name and namespace,  
                                // though this isn't recommended  
                                //XmlNodeList nodes = doc.GetElementsByTagName("forecast",   
                                //                          "http://xml.weather.yahoo.com/ns/rss/1.0");  
                                foreach (XmlNode node2 in nodes2)
                                {
                                    output = node2.Attributes["city"].InnerText + ", " + node2.Attributes["region"].InnerText + ": ";
                                }
                                foreach (XmlNode node in nodes)
                                {
                                    output += node.Attributes["text"].InnerText + " " + node.Attributes["temp"].InnerText + "F ";
                                }
                            }
                            else
                            {
                                output = zip_code + " is not a valid zip code";
                            }
                            sendData("PRIVMSG", ex[2] + " :" +output);
                            //if the command is !say, send a message to the chan (ex[2]) followed by the actual message (ex[4]).
                            break;
                        case ":!quit":
                            sendData("QUIT", ex[4]);
                            //if the command is quit, send the QUIT command to the server with a quit message
                            shouldRun = false;
                            //turn shouldRun to false - the server will stop sending us data so trying to read it will not work and result in an error. This stops the loop from running and we will close off the connections properly
                            break;
                    }
                }
            }
        }

        public void Dispose()
        {
            if (sr != null)
                sr.Close();
            if (sw != null)
                sw.Close();
            if (ns != null)
                ns.Close();
            if (IRCConnection != null)
                IRCConnection.Close();
        }
    }


    internal class Program
    {
        private static void Main(string[] args)
        {
            IRCConfig conf = new IRCConfig();
            conf.name = "DrPepper";
            conf.nick = "DrPepper";
            conf.port = 6667;
            conf.channel = "#downfall";
            conf.server = "irc.quakenet.org";
            using (var bot = new IRCBot(conf))
            {
                conf.joined = false;
                bot.Connect();
                bot.IRCWork();
            }
            Console.WriteLine("Bot quit/crashed");
            Console.ReadLine();
        }
    }

}
