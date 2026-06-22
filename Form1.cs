using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using EasyModbus;
using System.Configuration;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using uPLibrary.Networking.M2Mqtt.Exceptions;
using uPLibrary.Networking.M2Mqtt.Session;
using EasyModbus.Exceptions;

//using System.Text.Json;




namespace steamboxV3._0
{
    public partial class Form1 : Form
    {
        ModbusClient ModClient = new ModbusClient();
        private readonly object modbusLock = new object();

        public MqttClient mqClient;

        public Form1()
        {
            InitializeComponent();
            bacaconfig();
            bacaPort();
            mqtt_sub();

            // Initialize UI Arrays for the 15 Steamboxes
            InitializeUIArrays(15);
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void Form1_Closing(object sender, FormClosingEventArgs e)
        {
            mqClient.Disconnect();
            mqClient = null;
            ModClient.Disconnect();
        }


        //button trial kondisi
        private void button1_Click(object sender, EventArgs e) //send / publish mqtt
        {
            if (mqClient.IsConnected)
            {
                mqClient.Publish(topic_pub, Encoding.UTF8.GetBytes(textBox1.Text));
                textBox1.Text = "mqtt pub sukses";
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            for (byte i = 2; i < 4; i++)
            {
                if (sb_aktif[i] == 1)
                {
                    try
                    {
                        ModClient.UnitIdentifier = i;
                        ModClient.WriteSingleRegister(addr_alarm, 20);
                        ModClient.WriteSingleRegister(addr_sv, 100);
                    }
                    catch (TimeoutException)
                    {
                        richTextBox1.AppendText("button2_Click TimeoutException ..." + i + "\n\n");
                    }
                }
            }
        }


        // app.config
        string comport, baudrate, timeout;
        string ip, port, id, user, pass;
        string start_pemasakan, selisih_pemasakan, sv_on, alarm_on, alarm_off, timer_tick;
        // string active_ids; // Removed
        // List<int> active_id_list = new List<int>(); // Removed
        int[] skip_counter = new int[31];
        private int nextBatch = 1;

        void bacaconfig()
        {
            comport = ConfigurationManager.AppSettings["comport"].ToString();
            baudrate = ConfigurationManager.AppSettings["baudrate"].ToString();
            timeout = ConfigurationManager.AppSettings["timeout"].ToString();

            ip = ConfigurationManager.AppSettings["ip"].ToString();
            port = ConfigurationManager.AppSettings["port"].ToString();
            id = ConfigurationManager.AppSettings["id"].ToString();
            user = ConfigurationManager.AppSettings["user"].ToString();
            pass = ConfigurationManager.AppSettings["pass"].ToString();

            start_pemasakan = ConfigurationManager.AppSettings["start_pemasakan"].ToString();
            selisih_pemasakan = ConfigurationManager.AppSettings["selisih_pemasakan"].ToString();
            sv_on = ConfigurationManager.AppSettings["sv_on"].ToString();
            alarm_on = ConfigurationManager.AppSettings["alarm_on"].ToString();
            alarm_off = ConfigurationManager.AppSettings["alarm_off"].ToString();
            timer_tick = ConfigurationManager.AppSettings["timer_tick"].ToString();

            val_alarmOn = int.Parse(alarm_on);
            val_alarmOff = int.Parse(alarm_off);
            val_sv = int.Parse(sv_on);
            timer1.Interval = int.Parse(timer_tick);

            richTextBox1.Text = "System Error Log:\n";
            richTextBox2.Text = "MQTT Traffic Log:\n";
            richTextBox3.Text = "Action Log:\n";
            richTextBox4.Text = "Heartbeat Log:\n";

            richTextBox_status.AppendText("\nCOM PORT: " + comport + "\n");
            richTextBox_status.AppendText("SV: " + (val_sv / 10) + "\n");
            richTextBox_status.AppendText("AL1.h => Run: " + (val_alarmOn / 10) + ", Stop: " + (val_alarmOff / 10) + "\n\n");

            try
            {
                mqClient = new MqttClient(ip, 1884, false, null, null, MqttSslProtocols.TLSv1_2);
                mqClient.Connect(id, user, pass);

                if (mqClient.IsConnected)
                {
                    richTextBox_status.AppendText("MQTT_Server Connected\n");

                    mqtt_sub();
                }

            }
            catch (MqttConnectionException)
            {
                richTextBox_status.AppendText("MQTT_Server Not Detected\n");
            }

            for (byte i = 1; i < data_pub.Length; i++)
            {
                run_pub[i] = 0;
                temp_pub[i] = 0;
                com_pub[i] = 0;

                pv_val[i] = 0;
                sv_val[i] = 0;
                al1h_val[i] = 0;
                sb_aktif[i] = 1;

                mq_durasi[i] = 0;
                mq_resep[i] = "-";
            }

        }

        void bacaPort()
        {
            ModClient.SerialPort = comport;
            ModClient.Baudrate = int.Parse(baudrate);
            ModClient.StopBits = StopBits.Two;
            ModClient.Parity = Parity.None;
            ModClient.ConnectionTimeout = 250;

            try
            {
                ModClient.Connect();
                timer1.Start();
                richTextBox_status.AppendText("Modbus_Client Connected\n");
                scan_sb();
            }
            catch (System.IO.IOException e)
            {
                richTextBox_status.AppendText("Modbus_Client Not Detected\n");
                timer1.Stop();
            }

        }

        /****    MQTT     ****/
        string topic_pub = "sb/data";
        string topic_sub = "sb/req";

        //subcribe
        int mqid, mqdurasi, mqrun;
        int[] mq_durasi = new int[31];
        int[] mq_run = new int[31];
        string mqresep;
        string[] mq_resep = new string[31];
        int[] mq_flag = new int[31];


        //publish
        string[] data_pub = new string[31];
        int[] run_pub = new int[31];
        float[] temp_pub = new float[31];
        int[] com_pub = new int[31];
        // Removed shared strings: pemasakan_time, durasi_time
        string data;
        int tempnon, tempdes;


        public void mqtt_sub()
        {
            mqClient.MqttMsgPublishReceived += MQClient_MqttMsgPublishReceived;

            string[] topic = new string[1];
            topic[0] = topic_sub;

            byte[] msg = new byte[1];
            msg[0] = MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE;
            mqClient.Subscribe(topic, msg);
        }

        public void mqtt_pub()
        {
            if (mqClient.IsConnected)
            {
                data = "[";
                for (byte i = 1; i < data_pub.Length; i++)
                {
                    run_pub[i] = mq_run[i];
                    temp_pub[i] = pv_val[i] / 10;
                    com_pub[i] = sb_aktif[i];

                    tempnon = pv_val[i] / 10;
                    tempdes = pv_val[i] % 10;

                    data_pub[i] = "{\"id\": " + i + ",\"run\": " + run_pub[i] + ",\"temp\": " + tempnon + "." + tempdes + ",\"com\": " + com_pub[i] + "}";

                    data = data + data_pub[i];
                    if (i < (data_pub.Length - 1))
                    {
                        data = data + ",";
                    }
                }
                data = data + "]";
                mqClient.Publish(topic_pub, Encoding.UTF8.GetBytes(data));
                this.Invoke(new Action(() =>
                {
                    richTextBox2.Text = $"mqtt_pub()... [TICK] { DateTime.Now:HH: mm: ss}\n\n" + data;
                    textBox1.Text = "mqtt pub sukses";
                }));
                data = "";
            }
        }

        private void MQClient_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e) //receieve mqtt subs
        {
            string topic = e.Topic;
            string value = Encoding.UTF8.GetString(e.Message);

            if (topic == topic_sub)
            {
                string text = value;
                string[] datain = text.Split(new char[] { ':', ',', '}' }); // Splits by semicolon or comma

                try
                {
                    mqid = int.Parse(datain[1].Trim('"'));
                    mqdurasi = int.Parse(datain[5]);
                    mqresep = datain[3].Trim('"');
                    if (mqresep.Length > 11)
                    {
                        mqresep = mqresep.Substring(0, 11); // Extracts the first 14 characters
                    }
                    mqrun = int.Parse(datain[7].Trim('"'));

                    mq_run[mqid] = mqrun;
                    mq_durasi[mqid] = mqdurasi * 60; // Convert minutes to seconds
                    mq_resep[mqid] = mqresep;

                    mq_flag[mqid] = 1;
                    // Don't set sb_aktif = 1 here unless you are sure, or it might override a deliberate NA
                    // Let's keep it for compatibility with previous logic
                    sb_aktif[mqid] = 1;

                    this.Invoke(new Action(() =>
                    {
                        if (richTextBox2.Text.Length > 10000) richTextBox2.Text = "MQTT Traffic Log:\n";
                        richTextBox2.AppendText($"[SUB] ID:{mqid} Resep:{mqresep} Durasi:{mqdurasi} Run:{mqrun}\n");
                    }));
                }
                catch (IndexOutOfRangeException)
                {
                    richTextBox2.Invoke(new Action(() => richTextBox2.AppendText("IndexOutOfRangeException\n")));
                }
                catch (FormatException)
                {
                    richTextBox2.Invoke(new Action(() => richTextBox2.AppendText("FormatException\n")));
                }
                catch (NullReferenceException)
                {
                    richTextBox2.Invoke(new Action(() => richTextBox2.AppendText("NullReferenceException\n")));
                }
                catch (ArgumentOutOfRangeException)
                {
                    richTextBox2.Invoke(new Action(() => richTextBox2.AppendText("NullReferenceException\n")));
                }
                catch (TimeoutException)
                {
                    richTextBox2.Invoke(new Action(() => richTextBox2.AppendText("TimeoutException\n")));
                }
            }
        }


        /****    MODBUS     ****/
        //addr read value
        int flag_status = 50; //read coil register
        int flag_out1 = 3, flag_alarm1 = 9; //read discreat input

        int read_val_pv = 1000, read_val_sv = 1003; //read input register
        int read_val_al1h = 54; //read holding register

        //read instruction
        //ModClient.UnitIdentifier = i;
        //int[] status_flag = ModClient.ReadHoldingRegisters(flag_status, 4);
        //bool[] out1_flag = ModClient.ReadDiscreteInputs(flag_out1, 4);
        //bool[] alarm1_flag = ModClient.ReadDiscreteInputs(flag_alarm1, 4);

        //int[] pv_val = ModClient.ReadInputRegisters(read_val_pv, 4);
        //int[] sv_val = ModClient.ReadInputRegisters(read_val_sv, 4);
        //int[] al1h_val = ModClient.ReadInputRegisters(read_val_al1h, 4);


        //addr write value -> write single register
        byte id_sb, addr_run = 50, addr_alarm = 54, addr_sv = 0;
        int val_run = 0, val_stop = 1, val_alarmOn, val_alarmOff, val_sv;
        byte sbmax = 16;

        //buffer read modbus (ALL SB)
        int[] status_buf = new int[31];
        int[] status_flag = new int[31];
        bool[] out1_flag = new bool[31];
        bool[] alarm1_flag = new bool[31];
        int[] pv_val = new int[31];
        int[] sv_val = new int[31];
        int[] al1h_val = new int[31];
        int[] sb_aktif = new int[31];
        bool[] sb_connected = new bool[31];

        //timer pemasakan counting
        int[] count_pemasakan = new int[31];
        int[] hours_pemasakan = new int[31];
        int[] minutes_pemasakan = new int[31];
        int[] seconds_pemasakan = new int[31];
        string pemasakan_time;

        //time durasi converter
        int[] hours_durasi = new int[31];
        int[] minutes_durasi = new int[31];
        int[] seconds_durasi = new int[31];
        string durasi_time;

        /****    MODBUS Func For Control Steambox / TK4     ****/
        async void run_stop() //run/stop via button
        {
            if (ModClient.Connected == true)
            {
                byte currentId = id_sb;
                // Disable clicking if not connected
                if (!sb_connected[currentId] || sb_aktif[currentId] == 0) return;

                await Task.Run(() =>
                {
                    lock (modbusLock)
                    {
                        try
                        {
                            ModClient.UnitIdentifier = currentId;
                            status_buf = ModClient.ReadHoldingRegisters(flag_status, 1);
                            status_flag[currentId] = status_buf[0];

                            if (status_flag[currentId] == 1)
                            {
                                run(currentId);
                                count_pemasakan[currentId] = 0;
                                mq_run[currentId] = 1;
                                mq_durasi[currentId] = 0;
                                mq_resep[currentId] = "-";

                                this.Invoke(new Action(() =>
                                {
                                    if (richTextBox3.Text.Length > 5000) richTextBox3.Text = "Action Log:\n";
                                    richTextBox3.AppendText($"[CMD] ID {currentId} -> RUN | Resep: {mq_resep[currentId]} | Durasi: {mq_durasi[currentId]}\n");
                                }));

                                dt_hour[currentId] = 0;
                                dt_minute[currentId] = 0;
                                dt_second[currentId] = 0;
                            }
                            else if (status_flag[currentId] == 0)
                            {
                                stop(currentId);
                                mq_run[currentId] = 0;
                                this.Invoke(new Action(() =>
                                {
                                    richTextBox1.Text = "run_stop >> STOP..." + currentId + "\n\n";
                                    richTextBox3.AppendText("run_stop.. " + currentId + " Resep: " + mq_resep[currentId] + " Durasi: " + mq_resep[currentId] + "\n");
                                }));
                            }
                        }
                        catch (Exception ex)
                        {
                            this.Invoke(new Action(() => richTextBox1.AppendText("Error: " + ex.Message + "\n")));
                        }
                    }
                });
            }
        }

        void mqrun_stop(byte id)
        {
            if (mq_run[id] == 1 && status_flag[id] == 1)
            {
                run(id);
                count_pemasakan[id] = 0;
                this.Invoke(new Action(() =>
                {
                    richTextBox2.AppendText(mq_run[id] + "mqrun ok.....\n");
                    richTextBox4.Text = "SB_" + id + "\nMQTT_State: " + mq_run[id].ToString() + "\nMQ Run ok.....\n";
                }));
            }
            else if (mq_run[id] == 0 && status_flag[id] == 0)
            {
                stop(id);
                this.Invoke(new Action(() =>
                {
                    richTextBox2.AppendText(mq_run[id] + "mqstop ok...\n");
                    richTextBox4.Text = "SB_" + id + "\nMQTT_State: " + mq_run[id].ToString() + "\nMQ Stop ok.....\n";
                }));
            }
        } //run/stop via web app

        void run(byte id)
        {
            lock (modbusLock)
            {
                ModClient.UnitIdentifier = id;
                ModClient.WriteSingleRegister(addr_run, val_run);
                ModClient.WriteSingleRegister(addr_sv, val_sv);
                ModClient.WriteSingleRegister(addr_alarm, val_alarmOn);
            }
            mq_run[id] = 1;

            // Reset cooking timer for a fresh run
            start_time_pemasakan[id] = DateTime.MinValue;
            pemasakan_time_strings[id] = "00 : 00 : 00";

            dt_hour[id] = 0;
            dt_minute[id] = 0;
            dt_second[id] = 0;
        }

        void stop(byte id)
        {
            lock (modbusLock)
            {
                ModClient.UnitIdentifier = id;
                ModClient.WriteSingleRegister(addr_run, val_stop);
                ModClient.WriteSingleRegister(addr_sv, val_sv);
                ModClient.WriteSingleRegister(addr_alarm, val_alarmOff);
            }
            mq_run[id] = 0;

            // Reset cooking timer when stopped
            // start_time_pemasakan[id] = DateTime.MinValue;
            // pemasakan_time_strings[id] = "00 : 00 : 00";
        }

        async void scan_sb()
        {
            await Task.Run(() =>
            {
                string foundIds = "";
                for (byte i = 1; i < sbmax; i++)
                {
                    try
                    {
                        lock (modbusLock)
                        {
                            System.Threading.Thread.Sleep(50);
                            ModClient.UnitIdentifier = i;

                            // Try to read status to confirm presence using original 4-reg pattern
                            ModClient.ReadHoldingRegisters(50, 4);
                            sb_aktif[i] = 1;
                            foundIds += i + " ";
                        }
                    }
                    catch (Exception)
                    {
                        sb_aktif[i] = 0;
                        sb_connected[i] = false;

                        // Hard Reset on scan failure to clear bus for next ID
                        lock (modbusLock)
                        {
                            try
                            {
                                if (ModClient.Connected) ModClient.Disconnect();
                                System.Threading.Thread.Sleep(100);
                                ModClient.Connect();
                            }
                            catch { }
                        }
                        System.Threading.Thread.Sleep(50);
                    }
                }
                this.Invoke(new Action(() =>
                {
                    if (richTextBox4.Text.Length > 5000) richTextBox4.Text = "Heartbeat Log:\n";
                    richTextBox4.AppendText($"[SCAN] {DateTime.Now:HH:mm:ss} | Found: {foundIds}\n");
                }));
            });

            // Force a UI update for all units immediately after scan
            this.Invoke(new Action(() => {
                for (byte i = 1; i < sbmax; i++) {
                    ProcessSteambox(i);
                }
            }));
        }



        void readval_single(byte id)
        {
            if (sb_aktif[id] == 0) return;

            try
            {
                lock (modbusLock)
                {
                    ModClient.UnitIdentifier = id;

                    // Read 1: Status (Matches pattern in original scan)
                    int[] h_regs = ModClient.ReadHoldingRegisters(50, 4);
                    status_flag[id] = h_regs[0];
                    System.Threading.Thread.Sleep(20);

                    // Read 2: PV and SV
                    int[] i_regs = ModClient.ReadInputRegisters(1000, 4);
                    pv_val[id] = i_regs[0];
                    sv_val[id] = i_regs[3];
                    System.Threading.Thread.Sleep(20);

                    // Read 3: Discreet Inputs
                    out1_flag[id] = ModClient.ReadDiscreteInputs(3, 1)[0];
                    alarm1_flag[id] = ModClient.ReadDiscreteInputs(9, 1)[0];
                    System.Threading.Thread.Sleep(20);

                    // Read 4: AL1H
                    int[] al_regs = ModClient.ReadHoldingRegisters(54, 4);
                    al1h_val[id] = al_regs[0];
                }

                sb_connected[id] = true;
                cek_al1h(id);
            }
            catch (Exception ex)
            {
                sb_connected[id] = false;
                // CRITICAL: DO NOT set sb_aktif = 0. Keeping it as 1 ensures it will try again next cycle.

                this.Invoke(new Action(() =>
                {
                    if (richTextBox1.Text.Length > 5000) richTextBox1.Text = "System Error Log:\n";
                    richTextBox1.AppendText($"[MODBUS] ID {id} fail: {ex.Message}\n");
                }));

                // HARD RESET: If any error occurs, the bus might have noise.
                // We bounce the connection VERY quickly to ensure the UART is clear for the next ID.
                lock (modbusLock)
                {
                    try
                    {
                        if (ModClient.Connected) ModClient.Disconnect();
                        System.Threading.Thread.Sleep(150); // Give the RS485 converter 'air'
                        ModClient.Connect();
                    }
                    catch { }
                }
            }

            /** Update Data For MQTT Pub Buffer **/
            run_pub[id] = (status_flag[id] == 0) ? 1 : 0;
            temp_pub[id] = pv_val[id];
            com_pub[id] = sb_aktif[id];
        }

        void cek_al1h(byte id)
        {
            lock (modbusLock)
            {
                //sv_on, alarm_on, alarm_off
                if (sv_val[id] != int.Parse(sv_on)) //if run
                {
                    ModClient.WriteSingleRegister(addr_sv, val_sv);//write ulang sv
                }

                if (al1h_val[id] != int.Parse(alarm_on) && status_flag[id] == 0) //if run
                {
                    ModClient.WriteSingleRegister(addr_alarm, val_alarmOn); //write ulang al1h
                }
                else if (al1h_val[id] != int.Parse(alarm_off) && status_flag[id] == 1) //if stop
                {
                    ModClient.WriteSingleRegister(addr_alarm, val_alarmOff);
                }
            }
        }



        /****    Run Stop manual by button     ****/

        private void btn_close_Click(object sender, EventArgs e)
        {
            mqClient.Disconnect();
            mqClient = null;
            ModClient.Disconnect();
        }

        private bool isScanning = false;
        private async void btn_scanSb_Click(object sender, EventArgs e)
        {
            if (isScanning) return;
            isScanning = true;
            btn_scanSb.Enabled = false;

            await Task.Run(() => scan_sb());

            btn_scanSb.Enabled = true;
            isScanning = false;
        }
        private void btn_status1_Click(object sender, EventArgs e)
        {
            id_sb = 1;
            run_stop();
        }
        private void btn_status2_Click(object sender, EventArgs e)
        {
            id_sb = 2;
            run_stop();
        }
        private void btn_status3_Click(object sender, EventArgs e)
        {
            id_sb = 3;
            run_stop();
        }
        private void btn_status4_Click(object sender, EventArgs e)
        {
            id_sb = 4;
            run_stop();
        }
        private void btn_status5_Click(object sender, EventArgs e)
        {
            id_sb = 5;
            run_stop();
        }
        private void btn_status6_Click(object sender, EventArgs e)
        {
            id_sb = 6;
            run_stop();
        }
        private void btn_status7_Click(object sender, EventArgs e)
        {
            id_sb = 7;
            run_stop();
        }
        private void btn_status8_Click(object sender, EventArgs e)
        {
            id_sb = 8;
            run_stop();
        }
        private void btn_status9_Click(object sender, EventArgs e)
        {
            id_sb = 9;
            run_stop();
        }
        private void btn_status10_Click(object sender, EventArgs e)
        {
            id_sb = 10;
            run_stop();
        }
        private void btn_status11_Click(object sender, EventArgs e)
        {
            id_sb = 11;
            run_stop();
        }

        private void btn_status12_Click(object sender, EventArgs e)
        {
            id_sb = 12;
            run_stop();
        }
        private void btn_status13_Click(object sender, EventArgs e)
        {
            id_sb = 13;
            run_stop();
        }
        private void btn_status14_Click(object sender, EventArgs e)
        {
            id_sb = 14;
            run_stop();
        }
        private void btn_status15_Click(object sender, EventArgs e)
        {
            id_sb = 15;
            run_stop();
        }


        /****    Update GUI per Timer Tick     ****/
        //float a, b, c;
        int tim, tim1, tim2, next = 1;

        private bool isProcessing = false;

        private async void timer1_Tick(object sender, EventArgs e)
        {
            // 1. Prevent overlapping OR interruption during scanning
            if (isProcessing || isScanning) return;

            if (ModClient.Connected)
            {
                isProcessing = true;
                try
                {
                    // Offload the entire loop to a background task to keep the UI responsive
                    await Task.Run(() =>
                    {
                        this.Invoke(new Action(() =>
                        {
                            richTextBox3.Text = "";
                        }));
                        // Strictly Sequential Polling for ONLY found devices
                        for (byte i = 1; i < sbmax; i++)
                        {
                            if (sb_aktif[i] == 1)
                            {
                                ProcessSteambox(i);
                                // Small gap so we don't spam the RS485 line
                                System.Threading.Thread.Sleep(50);
                                this.Invoke(new Action(() =>
                                {
                                    richTextBox3.AppendText($"[TICK] {DateTime.Now:HH:mm:ss} | sb_aktif:" + i + "\n");
                                }));
                            }
                        }

                        this.Invoke(new Action(() =>
                        {
                            if (richTextBox4.Text.Length > 5000) richTextBox4.Text = "Heartbeat Log:\n";
                            richTextBox4.AppendText($"[TICK] {DateTime.Now:HH:mm:ss} | Poll Cycle Complete\n");
                        }));

                        mqtt_pub();
                    });
                }
                catch (Exception) { }
                finally
                {
                    isProcessing = false;
                }
            }
            else
            {
                // Master Modbus Disconnected - Update all units to show 'No Comm' and disable buttons
                for (byte i = 1; i < sbmax; i++)
                {
                    ProcessSteambox(i);
                }
            }
        }







        int[] dt_hour = new int[31];
        int[] dt_minute = new int[31];
        int[] dt_second = new int[31];

        DateTime date;
        //byte[] date_flag = new byte[31];

        int[] val_hour = new int[31];
        int[] val_minute = new int[31];
        int[] val_second = new int[31];
        int[] val_dt = new int[31];

        private void mulai_pemasakan()
        {
            date = DateTime.Now;
            dt_hour[id_sb] = date.Hour;
            dt_minute[id_sb] = date.Minute;
            dt_second[id_sb] = date.Second;

            val_hour[id_sb] = date.Hour * 3600;
            val_minute[id_sb] = date.Minute * 60;
            val_second[id_sb] = date.Second;
            val_dt[id_sb] = val_hour[id_sb] + val_minute[id_sb] + val_second[id_sb];
            timer_pemasakan2();
        }

        private void timer_pemasakan2()
        {
            DateTime date2 = DateTime.Now;
            int val_dtnow = (date2.Hour * 3600) + (date2.Minute * 60) + date2.Second;
            int interval = val_dtnow - val_dt[id_sb];

            hours_pemasakan[id_sb] = interval / 3600;
            minutes_pemasakan[id_sb] = (interval % 3600) / 60;
            seconds_pemasakan[id_sb] = interval % 60;
            pemasakan_time = hours_pemasakan[id_sb] + " : " + minutes_pemasakan[id_sb] + " : " + seconds_pemasakan[id_sb];

        }

        private void timer_pemasakan(int value, byte id)
        {
            //value = count_pemasakan[id_sb] * (1000 / int.Parse(timer_tick));

            hours_pemasakan[id] = value / 3600;
            minutes_pemasakan[id] = (value % 3600) / 60;
            seconds_pemasakan[id] = value % 60;
            pemasakan_time = hours_pemasakan[id] + " : " + minutes_pemasakan[id] + " : " + seconds_pemasakan[id];
        }

        private string get_durasi_formatted(int value, byte id)
        {
            hours_durasi[id] = value / 3600;
            minutes_durasi[id] = (value % 3600) / 60;
            seconds_durasi[id] = value % 60;

            if (value == 0) return "-";
            return string.Format("{0:00} : {1:00} : {2:00}", hours_durasi[id], minutes_durasi[id], seconds_durasi[id]);
        }

        // Add this to your class variables
        DateTime[] start_time_pemasakan = new DateTime[31];
        string[] pemasakan_time_strings = new string[31]; // To store the result for the GUI

        private void mulai_pemasakan(byte id)
        {
            // Record the exact moment cooking started
            start_time_pemasakan[id] = DateTime.Now;

            // Initial update
            update_timer_pemasakan(id);
        }

        private void update_timer_pemasakan(byte id)
        {
            if (start_time_pemasakan[id] == DateTime.MinValue) return;

            // Calculate the difference between "Now" and "Start"
            TimeSpan elapsed = DateTime.Now - start_time_pemasakan[id];

            // Format: 00 : 00 : 00
            pemasakan_time_strings[id] = string.Format("{0:00} : {1:00} : {2:00}",
                (int)elapsed.TotalHours,
                elapsed.Minutes,
                elapsed.Seconds);
        }

        private void pemasakan_off(byte id)
        {
            if (start_time_pemasakan[id] == DateTime.MinValue) return;

            // Get actual seconds elapsed
            double elapsedSeconds = (DateTime.Now - start_time_pemasakan[id]).TotalSeconds;
            int targetDurasi = mq_durasi[id];

            // selisih_pemasakan usually in minutes, so convert to seconds
            int allowedOvertimeSeconds = int.Parse(selisih_pemasakan) * 60;

            if (targetDurasi > 50 && elapsedSeconds > (targetDurasi + allowedOvertimeSeconds))
            {
                stop(id);
                this.Invoke(new Action(() => richTextBox2.AppendText($"SB {id} force off: Elapsed {elapsedSeconds}s > Limit {targetDurasi + allowedOvertimeSeconds}s\n")));

                // Reset start time so it doesn't keep triggering
                start_time_pemasakan[id] = DateTime.MinValue;
            }
        }

        //private void pemasakan_off() //force off berdasarkan selisih pemasakan 
        //{
        //    int count = count_pemasakan[id_sb] * (1000 / int.Parse(timer_tick));
        //    int durasi = mq_durasi[id_sb];

        //    int selisih = count - durasi;

        //    if (selisih > (int.Parse(selisih_pemasakan) * 60) && durasi > 50)
        //    {
        //        stop();
        //        richTextBox2.AppendText(id_sb + "force off ok...\n");
        //    }
        //}


        // Change the size to 31 so that index 1 to 30 are valid

        Label[] lbl_resep_all = new Label[31];
        Label[] lbl_durasi_all = new Label[31];
        Label[] lbl_sv_all = new Label[31];
        Label[] lbl_suhu_all = new Label[31];
        Label[] lbl_pemasakan_all = new Label[31];
        Label[] lbl_alh_all = new Label[31];

        Button[] btn_koneksi_all = new Button[31];
        Button[] btn_status_all = new Button[31];
        Button[] btn_out_all = new Button[31];
        Button[] btn_alarm_all = new Button[31];

        void InitializeUIArrays(int count)
        {
            for (int i = 1; i <= count; i++)
            {
                // Finding Labels by Name
                lbl_resep_all[i] = this.Controls.Find("lbl_resep" + i, true).FirstOrDefault() as Label;
                lbl_durasi_all[i] = this.Controls.Find("lbl_durasi" + i, true).FirstOrDefault() as Label;
                lbl_sv_all[i] = this.Controls.Find("lbl_sv" + i, true).FirstOrDefault() as Label;
                lbl_suhu_all[i] = this.Controls.Find("lbl_suhu" + i, true).FirstOrDefault() as Label;
                lbl_pemasakan_all[i] = this.Controls.Find("lbl_pemasakan" + i, true).FirstOrDefault() as Label;
                lbl_alh_all[i] = this.Controls.Find("lbl_alh" + i, true).FirstOrDefault() as Label;

                // Finding Buttons by Name
                btn_koneksi_all[i] = this.Controls.Find("btn_koneksi" + i, true).FirstOrDefault() as Button;
                btn_status_all[i] = this.Controls.Find("btn_status" + i, true).FirstOrDefault() as Button;
                btn_out_all[i] = this.Controls.Find("btn_out" + i, true).FirstOrDefault() as Button;
                btn_alarm_all[i] = this.Controls.Find("btn_alarm" + i, true).FirstOrDefault() as Button;

                // Debugging check: If a button isn't found, it will warn you
                if (btn_koneksi_all[i] == null)
                {
                    Console.WriteLine("Warning: Could not find btn_koneksi" + i);
                }
            }
        }

        void ProcessSteambox(byte id)
        {
            // Safety check to ensure the UI arrays were initialized for this ID
            if (id >= lbl_resep_all.Length || lbl_resep_all[id] == null) return;

            try
            {
                // Modbus Communication (Running on background thread)
                if (ModClient.Connected && sb_aktif[id] == 1)
                {
                    readval_single(id);
                }

                // 1. MQTT Command Logic
                if (mq_flag[id] == 1)
                {
                    mqrun_stop(id);
                    mq_flag[id] = 0;
                }

                // 2. Recipe & Duration Logic
                if (mq_resep[id] == null) mq_resep[id] = "-";

                // 7. Pemasakan (Cooking) Timer Logic
                if (pv_val[id] >= float.Parse(start_pemasakan) && status_flag[id] == 0)
                {
                    if (start_time_pemasakan[id] == DateTime.MinValue)
                    {
                        mulai_pemasakan(id);
                    }
                    else
                    {
                        update_timer_pemasakan(id);
                    }
                }

                // Update UI for this specific unit
                this.Invoke(new Action(() =>
                {
                    // Calculate display strings
                    string display_pemasakan = "00 : 00 : 00";
                    if (start_time_pemasakan[id] != DateTime.MinValue)
                    {
                        display_pemasakan = pemasakan_time_strings[id] + " / " + start_time_pemasakan[id].ToString("HH:mm:ss");
                    }

                    string current_durasi = get_durasi_formatted(mq_durasi[id], id);
                    string display_durasi = current_durasi;
                    if (start_time_pemasakan[id] != DateTime.MinValue && mq_durasi[id] > 0)
                    {
                        DateTime estimatedStop = start_time_pemasakan[id].AddSeconds(mq_durasi[id]);
                        display_durasi = current_durasi + " / " + estimatedStop.ToString("HH:mm:ss");
                    }

                    // 1. Update Labels
                    lbl_resep_all[id].Text = mq_resep[id]?.ToString() ?? "-";
                    lbl_durasi_all[id].Text = display_durasi;
                    lbl_sv_all[id].Text = (sv_val[id] / 10).ToString() + "." + (sv_val[id] % 10).ToString();
                    lbl_alh_all[id].Text = (al1h_val[id] / 10).ToString() + "." + (al1h_val[id] % 10).ToString();
                    lbl_suhu_all[id].Text = (pv_val[id] / 10).ToString() + "." + (pv_val[id] % 10).ToString();

                    // 2. Connection Status & Button Enable/Disable
                    if (ModClient.Connected && sb_aktif[id] == 1)
                    {
                        if (sb_connected[id])
                        {
                            btn_koneksi_all[id].Text = "Connected";
                            btn_koneksi_all[id].BackColor = Color.Green;
                            btn_status_all[id].Enabled = true;

                            if (status_flag[id] == 1)
                            {
                                btn_status_all[id].Text = "Run";
                                btn_status_all[id].BackColor = Color.Green;
                            }
                            else
                            {
                                btn_status_all[id].Text = "Stop";
                                btn_status_all[id].BackColor = Color.Red;
                            }
                        }
                        else
                        {
                            btn_koneksi_all[id].Text = "Disconnected";
                            btn_koneksi_all[id].BackColor = Color.Gray;
                            btn_status_all[id].Enabled = false;
                            btn_status_all[id].BackColor = Color.Gray;
                        }
                    }
                    else if (sb_aktif[id] == 0)
                    {
                        btn_koneksi_all[id].Text = "NA";
                        btn_koneksi_all[id].BackColor = Color.Black;
                        btn_status_all[id].Enabled = false;
                        btn_status_all[id].BackColor = Color.DarkGray;
                    }
                    else
                    {
                        // Master Modbus Disconnected
                        btn_koneksi_all[id].Text = "No Comm";
                        btn_koneksi_all[id].BackColor = Color.DarkRed;
                        btn_status_all[id].Enabled = false;
                        btn_status_all[id].BackColor = Color.Gray;
                    }

                    // 3. Indicator Lamps
                    if (ModClient.Connected && sb_aktif[id] == 1 && sb_connected[id])
                    {
                        btn_out_all[id].BackColor = out1_flag[id] ? Color.DarkGreen : Color.Red;
                        btn_alarm_all[id].BackColor = alarm1_flag[id] ? Color.DarkGreen : Color.Red;
                    }
                    else
                    {
                        btn_out_all[id].BackColor = Color.DarkGray;
                        btn_alarm_all[id].BackColor = Color.DarkGray;
                    }

                    lbl_pemasakan_all[id].Text = display_pemasakan;
                }));
            }
            catch (Exception)
            {
                this.Invoke(new Action(() =>
                {
                    btn_koneksi_all[id].Text = "Error";
                    btn_koneksi_all[id].BackColor = Color.Red;
                    btn_status_all[id].Enabled = false;
                }));
            }
        }
    }

}

