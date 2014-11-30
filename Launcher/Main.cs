﻿using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;

namespace LOG.Launcher
{
    public partial class Main : Form
    {
        public string KSPDirectory, LOGDirectory, ServerListPath;
        
        private string ServerIP = null, ServerPort = null;

        private DataTable DT = new DataTable();

        static bool is64BitProcess = (IntPtr.Size == 8);
        static bool is64BitOperatingSystem = is64BitProcess || InternalCheckIsWow64();

        #region Get Informations About Windows
        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(
            [In] IntPtr hProcess,
            [Out] out bool wow64Process
        );

        public static bool InternalCheckIsWow64()
        {
            if ((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) ||
                Environment.OSVersion.Version.Major >= 6)
            {
                using (Process p = Process.GetCurrentProcess())
                {
                    bool retVal;
                    if (!IsWow64Process(p.Handle, out retVal))
                    {
                        return false;
                    }
                    return retVal;
                }
            }
            else
            {
                return false;
            }
        }
        #endregion

        public Main()
        {
            InitializeComponent();
            
            KSPDirectory = Application.StartupPath;
            LOGDirectory = Path.Combine(KSPDirectory, "L.O.G");
            ServerListPath = Path.Combine(LOGDirectory, "ServerList.list");
        }

        private void Main_Load(object sender, EventArgs e)
        {
            if (!Directory.Exists(LOGDirectory))
                Directory.CreateDirectory(LOGDirectory);

            if (File.Exists(ServerListPath) == true && new FileInfo(ServerListPath).Length != 0)
            {
                LauncherNetwork.FirstLaunch();
                LoadingServersWorker.RunWorkerAsync();
            }
        }

        public void PlayButton_Click(object sender, EventArgs e)
        {
            ServerIP = ServerInfo.ServerDetail[ListOfServers.CurrentCell.RowIndex].IPAddress;
            ServerPort = ServerInfo.ServerDetail[ListOfServers.CurrentCell.RowIndex].Port.ToString();

            if (ServerIP.Length > 0 && ServerPort.Length > 0)
            {
                if (is64BitOperatingSystem == true)
                    Process.Start(Path.Combine(KSPDirectory, "KSP_x64.exe"), string.Format("-IP={0} -Port={1} -Username={2}", ServerIP, ServerPort, UserNameBox.Text));
                else
                    Process.Start(Path.Combine(KSPDirectory, "KSP.exe"), string.Format("-IP={0} -Port={1} -Username={2}", ServerIP, ServerPort, UserNameBox.Text));
            }
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            if (Program.addserver.IsDisposed == true)
                Program.addserver = new AddServer();

            Program.addserver.ShowDialog(this);
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            RefreshServerInfo();
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            string[] ServerListOld = File.ReadAllLines(ServerListPath);
            File.WriteAllLines(ServerListPath, ServerListOld.Where(line => !line.Contains(ServerInfo.ServerDetail[ListOfServers.CurrentCell.RowIndex].IPAddress)));

            ListOfServers.Rows.RemoveAt(ListOfServers.CurrentCell.RowIndex);
        }

        private void LoadingServersWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            LoadServerList();
        }

        private void LoadingServersWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if(ListOfServers.RowCount > 0)
                ListOfServers.Rows[0].Selected = true;
        }

        private void ListOfServers_Click(object sender, EventArgs e)
        {
            RefreshServerInfo();
        }

        public void LoadServerList()
        {
            if (ListOfServers.RowCount > 0)
                ListOfServers.Rows.Clear();

            int i, j;

            Dictionary<string, string> ResponseParts = new Dictionary<string, string>();

            string[] ServerList = File.ReadAllLines(ServerListPath), Server, ResponseMessageParts, ResponseFinalParts;

            for (i = 0; i < ServerList.Length; i++)
            {
                Server = ServerList[i].Split(':');

                string DiscoveredServerResponse = LauncherNetwork.DiscoverServers(Server[0], Convert.ToInt32(Server[1]));

                ResponseMessageParts = DiscoveredServerResponse.Split('#');

                for (j = 0; j < ResponseMessageParts.Length; j++)
                {
                    ResponseFinalParts = ResponseMessageParts[j].Split(':');
                    ResponseParts.Add(ResponseFinalParts[0], ResponseFinalParts[1]);
                }
                
                ServerInfo.ServerDetail.Add(new ServerInfo()
                {
                    IPAddress = Server[0],
                    Port = Convert.ToInt32(Server[1]),
                    HostName = ResponseParts["HostName"],
                    Players = Convert.ToInt32(ResponseParts["Players"]),
                    MaxPlayers = Convert.ToInt32(ResponseParts["MaxPlayers"]),
                    Ping = Convert.ToInt32(ResponseParts["Ping"])
                });

                ResponseParts = new Dictionary<string, string>();
            }

            foreach (ServerInfo serverinfo in ServerInfo.ServerDetail)
                ListOfServers.Invoke(new MethodInvoker(() => ListOfServers.Rows.Add(serverinfo.HostName, serverinfo.Players + " / " + serverinfo.MaxPlayers, serverinfo.Ping)));
        }

        private void RefreshServerInfo()
        {
            if (ListOfServers.SelectedRows.Count > 0)
            {
                Dictionary<string, string> ResponseParts = new Dictionary<string, string>();


                string[] ResponseMessageParts, ResponseFinalParts;
                string DiscoveredServerResponse = LauncherNetwork.DiscoverServers(ServerInfo.ServerDetail[ListOfServers.CurrentCell.RowIndex].IPAddress, Convert.ToInt32(ServerInfo.ServerDetail[ListOfServers.CurrentCell.RowIndex].Port));

                ResponseMessageParts = DiscoveredServerResponse.Split('#');

                for (int i = 0; i < ResponseMessageParts.Length; i++)
                {
                    ResponseFinalParts = ResponseMessageParts[i].Split(':');
                    ResponseParts.Add(ResponseFinalParts[0], ResponseFinalParts[1]);
                }

                ServerInfo.ServerDetail[ListOfServers.CurrentCell.RowIndex].HostName = ResponseParts["HostName"];
                ServerInfo.ServerDetail[ListOfServers.CurrentCell.RowIndex].Players = Convert.ToInt32(ResponseParts["Players"]);
                ServerInfo.ServerDetail[ListOfServers.CurrentCell.RowIndex].MaxPlayers = Convert.ToInt32(ResponseParts["MaxPlayers"]);
                ServerInfo.ServerDetail[ListOfServers.CurrentCell.RowIndex].Ping = Convert.ToInt32(ResponseParts["Ping"]);

                ListOfServers.SelectedRows[0].Cells[0].Value = ServerInfo.ServerDetail[ListOfServers.CurrentCell.RowIndex].HostName;
                ListOfServers.SelectedRows[0].Cells[1].Value = ServerInfo.ServerDetail[ListOfServers.CurrentCell.RowIndex].Players + " / " + ServerInfo.ServerDetail[ListOfServers.CurrentCell.RowIndex].MaxPlayers;
                ListOfServers.SelectedRows[0].Cells[2].Value = ServerInfo.ServerDetail[ListOfServers.CurrentCell.RowIndex].Ping;
            }
        }
    }
}
