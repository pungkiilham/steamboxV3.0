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

//using System.Text.Json;




namespace steamboxV3._0
{
    public partial class Form1 : Form
    {
        ModbusClient ModClient = new ModbusClient();

        public MqttClient mqClient;

        public Form1()
        {
            InitializeComponent();
            //richTextBox1.AppendText("System Start\n");// " + Environment.Version + "
            bacaconfig();
            bacaPort();
            mqtt_sub();
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

            richTextBox1.AppendText("\nSV: " + (val_sv / 10) + "\n");
            richTextBox1.AppendText("AL1.h => Run: " + (val_alarmOn / 10) + ", Stop: " + (val_alarmOff / 10) + "\n\n");

            try
            {
                mqClient = new MqttClient(ip, 1884, false, null, null, MqttSslProtocols.TLSv1_2);
                mqClient.Connect(id, user, pass);

                if (mqClient.IsConnected)
                {
                    richTextBox1.AppendText("MQTT_Server Connected\n");

                    mqtt_sub();
                }

            }
            catch (MqttConnectionException)
            {
                richTextBox1.AppendText("MQTT_Server Not Detected\n");
            }

            for (byte i = 1; i < data_pub.Length; i++)
            {
                run_pub[i] = 0;
                temp_pub[i] = 0;
                com_pub[i] = 0;

                //status_flag[i] = 1;
                //out1_flag[i] = false;
                //alarm1_flag[i] = false;
                pv_val[i] = 0;
                sv_val[i] = 0;
                al1h_val[i] = 0;
                sb_aktif[i] = 1;

                mq_durasi[mqid] = 0;
                mq_resep[mqid] = "-";
            }

        }

        void bacaPort()
        {
            ModClient.SerialPort = comport;
            ModClient.Baudrate = int.Parse(baudrate);
            ModClient.StopBits = StopBits.Two;
            ModClient.Parity = Parity.None;
            ModClient.ConnectionTimeout = int.Parse(timeout);

            try
            {
                ModClient.Connect();
                timer1.Start();
                richTextBox1.AppendText("Modbus_Client Connected\n");
                //scan_sb();
            }
            catch (System.IO.IOException e)
            {
                richTextBox1.AppendText("Modbus_Client Not Detected\n");
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

        //publish
        string[] data_pub = new string[31];
        int[] run_pub = new int[31];
        float[] temp_pub = new float[31];
        int[] com_pub = new int[31];
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


                    //data_pub[i] = "{\"id\": " + i + ",\"run\": " + run_pub[i] + ",\"temp\": " + temp_pub[i] + ",\"com\": " + com_pub[i] + "}";
                    data_pub[i] = "{\"id\": " + i + ",\"run\": " + run_pub[i] + ",\"temp\": " + tempnon + "." + tempdes + ",\"com\": " + com_pub[i] + "}";
                    //mqClient.Publish(topic_pub, Encoding.UTF8.GetBytes(data_pub[i]));
                    data = data + data_pub[i];
                    if (i < (data_pub.Length - 1))
                    {
                        data = data + ",";
                    }
                }
                data = data + "]";
                mqClient.Publish(topic_pub, Encoding.UTF8.GetBytes(data));
                richTextBox2.Text = "mqtt_pub()...\n\n" + data;
                data = "";
                textBox1.Text = "mqtt pub sukses";
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
                    mq_durasi[mqid] = mqdurasi;
                    mq_resep[mqid] = mqresep;

                    mqrun_stop();
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

                //richTextBox2.Invoke(new Action(() => richTextBox2.Text = value + "\n\n"));
                //richTextBox2.Invoke(new Action(() => richTextBox2.AppendText("mqid: " + mqid + ", mqresep: " + mqresep + ", mqdurasi: " + mqdurasi + ", mqrun: " + mqrun + "\n\n")));

                for (int i = 1; i < mq_run.Length; i++)
                {
                    richTextBox2.Invoke(new Action(() => richTextBox2.AppendText("MQClient_MqttMsgPublishReceived...\n" + "id " + i + ", mq_run: " + mq_run[i] + ", mq_durasi: " + mq_durasi[i] + ", mq_resep: " + mq_resep[i] + "\n")));
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
        byte sbmax = 31;

        //buffer read modbus (SINGLE SB)
        int[] status_buf = new int[31];
        bool[] out1_buf = new bool[31];
        bool[] alarm1_buf = new bool[31];
        int[] pv_buf = new int[31];
        int[] sv_buf = new int[31];
        int[] al1h_buf = new int[31];


        //buffer read modbus (ALL SB)
        int[] status_flag = new int[31];
        bool[] out1_flag = new bool[31];
        bool[] alarm1_flag = new bool[31];
        int[] pv_val = new int[31];
        int[] sv_val = new int[31];
        int[] al1h_val = new int[31];
        int[] sb_aktif = new int[31];

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
        void run_stop() //run/stop via button
        {
            if (ModClient.Connected == true)
            {
                ModClient.UnitIdentifier = id_sb;
                status_buf = ModClient.ReadHoldingRegisters(flag_status, 4);
                status_flag[id_sb] = status_buf[0];

                if (status_flag[id_sb] == 1)
                {
                    ModClient.WriteSingleRegister(addr_run, val_run);
                    ModClient.WriteSingleRegister(addr_sv, val_sv);
                    ModClient.WriteSingleRegister(addr_alarm, val_alarmOn);
                    count_pemasakan[id_sb] = 0;
                    mq_run[id_sb] = 1;
                    mq_durasi[id_sb] = 0;
                    mq_resep[id_sb] = "-";
                    richTextBox1.Text = "run_stop >> RUN..." + id_sb + "\n\n";
                }
                else if (status_flag[id_sb] == 0)
                {
                    ModClient.WriteSingleRegister(addr_run, val_stop);
                    ModClient.WriteSingleRegister(addr_sv, val_sv);
                    ModClient.WriteSingleRegister(addr_alarm, val_alarmOff);
                    mq_run[id_sb] = 0;
                    richTextBox1.Text = "run_stop >> STOP..." + id_sb + "\n\n";
                }

                richTextBox3.AppendText("run_stop.. " + id_sb + " Resep: " + mq_resep[id_sb] + " Durasi: " + mq_resep[id_sb] + "\n");

            }
        }

        void mqrun_stop()
        {
            ModClient.UnitIdentifier = id_sb;
            status_buf = ModClient.ReadHoldingRegisters(flag_status, 4);
            status_flag[id_sb] = status_buf[0];


            if (mq_run[id_sb] == 1 && status_flag[id_sb] == 1)
            {
                run();
                count_pemasakan[id_sb] = 0;
                richTextBox2.AppendText(mq_run[id_sb] + "mqrun ok.....\n");
                textBox1.Text = mq_run[id_sb].ToString() + "mqrun ok.....\n";
            }
            else if (mq_run[id_sb] == 0 && status_flag[id_sb] == 0)
            {
                stop();
                richTextBox2.AppendText(mq_run[id_sb] + "mqstop ok...\n");
                textBox1.Text = mq_run[id_sb].ToString() + "mqrun ok.....\n";
            }
        } //run/stop via web app

        void run()
        {
            ModClient.UnitIdentifier = id_sb;
            ModClient.WriteSingleRegister(addr_run, val_run);
            ModClient.WriteSingleRegister(addr_sv, val_sv);
            ModClient.WriteSingleRegister(addr_alarm, val_alarmOn);
            mq_run[id_sb] = 1;

            //count_pemasakan[id_sb] = 0;
        }

        void stop()
        {
            ModClient.UnitIdentifier = id_sb;
            ModClient.WriteSingleRegister(addr_run, val_stop);
            ModClient.WriteSingleRegister(addr_sv, val_sv);
            ModClient.WriteSingleRegister(addr_alarm, val_alarmOff);
            mq_run[id_sb] = 0;
        }

        void scan_sb()
        {
            //int[] status_buf = new int[31];

            richTextBox1.Text = "scan sb sukses";

            for (byte i = 1; i < sbmax; i++)
            {
                try
                {
                    ModClient.UnitIdentifier = i;
                    status_buf = ModClient.ReadHoldingRegisters(flag_status, 4);
                    sb_aktif[i] = 1;
                    if (status_buf[0] == 1) //if stop
                    {
                        status_flag[i] = 1;
                    }
                    else if (status_buf[0] == 0) //if run
                    {
                        status_flag[i] = 0;
                    }
                }
                catch (TimeoutException)
                {
                    //status_flag[i] = 0;
                    //out1_flag[i] = true;
                    //alarm1_flag[i] = true;

                    //pv_val[i] = 0;
                    //sv_val[i] = 0;
                    al1h_val[i] = val_alarmOff;
                    sb_aktif[i] = 0;
                }
            }
        }

        void readval_multi()
        {
            //bacaPort();
            //richTextBox1.Text = "";


            for (byte i = 1; i < sbmax; i++)
            {
                try
                {
                    if (sb_aktif[i] == 1)
                    {
                        ModClient.UnitIdentifier = i;
                        status_buf = ModClient.ReadHoldingRegisters(flag_status, 4);
                        out1_buf = ModClient.ReadDiscreteInputs(flag_out1, 4);
                        alarm1_buf = ModClient.ReadDiscreteInputs(flag_alarm1, 4);

                        pv_buf = ModClient.ReadInputRegisters(read_val_pv, 4);
                        sv_buf = ModClient.ReadInputRegisters(read_val_sv, 4);
                        al1h_buf = ModClient.ReadHoldingRegisters(read_val_al1h, 4);

                        status_flag[i] = status_flag[0];
                        out1_flag[i] = out1_flag[0];
                        alarm1_flag[i] = alarm1_flag[0];

                        pv_val[i] = pv_val[0];
                        sv_val[i] = sv_val[0];
                        al1h_val[i] = al1h_val[0];
                        sb_aktif[i] = 1;

                        richTextBox1.AppendText(i + " status: " + status_flag[i] + ", out1_buf:" + out1_flag[i] +
                        ", alarm1_flag:" + alarm1_flag[i] + ", pv_val:" + pv_val[i] + ", sv_val:" + sv_val[i] +
                        ", al1h_val:" + al1h_val[i] + ", sb_aktif:" + sb_aktif[i] + "\n");

                        //a = sv_val[0];
                        //b = al1h_val[0];
                        //c = pv_val[0];
                    }
                }
                catch (TimeoutException)
                {
                    status_flag[i] = 0;
                    out1_flag[i] = false;
                    alarm1_flag[i] = false;

                    pv_val[i] = 0;
                    sv_val[i] = 0;
                    al1h_val[i] = 0;
                    sb_aktif[i] = 0;
                }
            }

            /** Update Data For MQTT Pub **/
            if (status_flag[0] == 0)
                run_pub[id_sb] = 1;
            else
                run_pub[id_sb] = 0;

            temp_pub[id_sb] = pv_val[id_sb];
            com_pub[id_sb] = 1;
            //mqtt_pub();
        }

        byte brploop;

        void readval_single()
        {
            richTextBox1.AppendText("cek sb_aktif.. " + id_sb + " .. " + sb_aktif[id_sb] + "\n");

            //bacaPort();
            if (brploop >= 10)
            {
                richTextBox1.Text = "";
                brploop = 0;
            }

            try
            {
                if (sb_aktif[id_sb] == 1)
                {
                    brploop++;
                    ModClient.UnitIdentifier = id_sb;
                    status_buf = ModClient.ReadHoldingRegisters(flag_status, 4);
                    out1_buf = ModClient.ReadDiscreteInputs(flag_out1, 4);
                    alarm1_buf = ModClient.ReadDiscreteInputs(flag_alarm1, 4);

                    pv_buf = ModClient.ReadInputRegisters(read_val_pv, 4);
                    sv_buf = ModClient.ReadInputRegisters(read_val_sv, 4);
                    al1h_buf = ModClient.ReadHoldingRegisters(read_val_al1h, 4);

                    status_flag[id_sb] = status_buf[0];
                    out1_flag[id_sb] = out1_buf[0];
                    alarm1_flag[id_sb] = alarm1_buf[0];

                    pv_val[id_sb] = pv_buf[0];
                    sv_val[id_sb] = sv_buf[0];
                    al1h_val[id_sb] = al1h_buf[0];
                    sb_aktif[id_sb] = 1;

                    richTextBox1.AppendText("readval_single.. " + id_sb + " status: " + status_flag[id_sb] + ", out1: " + out1_flag[id_sb] +
                    ", alarm1: " + alarm1_flag[id_sb] + ", pv: " + pv_val[id_sb] + ", sv: " + sv_val[id_sb] +
                    ", al1h: " + al1h_val[id_sb] + ", sb_aktif: " + sb_aktif[id_sb] + "\n");

                    cek_al1h();
                }
                else
                {
                    //pv_val[id_sb] = 0;
                    //sv_val[id_sb] = 0;
                    //al1h_val[id_sb] = 0;
                    sb_aktif[id_sb] = 0;
                    richTextBox1.AppendText("readval_single.. " + id_sb + " Disconnect " + "\n");
                }
            }

            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;
                richTextBox1.AppendText("readval_single.. " + id_sb + " Disconnect TimeoutException" + "\n");
            }


            /** Update Data For MQTT Pub **/
            if (status_flag[0] == 0)
                run_pub[id_sb] = 1;
            else
                run_pub[id_sb] = 0;

            temp_pub[id_sb] = pv_val[id_sb];
            com_pub[id_sb] = 1;
            mqtt_pub();
        }

        void cek_al1h()
        {
            //sv_on, alarm_on, alarm_off
            if (sv_val[id_sb] != int.Parse(sv_on)) //if run
            {
                ModClient.WriteSingleRegister(addr_sv, val_sv);//write ulang sv
            }

            if (al1h_val[id_sb] != int.Parse(alarm_on) && status_flag[id_sb] == 0) //if run
            {
                ModClient.WriteSingleRegister(addr_alarm, val_alarmOn); //write ulang al1h
                //richTextBox3.AppendText("cek_al1h.. " + id_sb + " Write Alarm_on"+ val_alarmOn + "\n");
            }
            else if (al1h_val[id_sb] != int.Parse(alarm_off) && status_flag[id_sb] == 1) //if stop
            {
                ModClient.WriteSingleRegister(addr_alarm, val_alarmOff);
                //richTextBox3.AppendText("cek_al1h.. " + id_sb + " Write Alarm_off: "+ val_alarmOff + "\n");
            }
        }

        private void timer_pemasakan(int value, byte id)
        {
            value = count_pemasakan[id_sb] * (1000 / int.Parse(timer_tick));

            hours_pemasakan[id] = value / 3600;
            minutes_pemasakan[id] = (value % 3600) / 60;
            seconds_pemasakan[id] = value % 60;
            pemasakan_time = hours_pemasakan[id] + " : " + minutes_pemasakan[id] + " : " + seconds_pemasakan[id];
        }

        private void durasi(int value, byte id)
        {
            hours_durasi[id] = value / 3600;
            minutes_durasi[id] = (value % 3600) / 60;
            seconds_durasi[id] = value % 60;

            if (value == 0)
                durasi_time = "-";
            else
                durasi_time = hours_durasi[id] + " : " + minutes_durasi[id] + " : " + seconds_durasi[id];
        }

        private void pemasakan_off() //force off berdasarkan selisih pemasakan 
        {
            int count = count_pemasakan[id_sb] * (1000 / int.Parse(timer_tick));
            int durasi = mq_durasi[id_sb];

            int selisih = count - durasi;

            if (selisih > (int.Parse(selisih_pemasakan) * 60) && durasi > 50)
            {
                stop();
                richTextBox2.AppendText(id_sb + "force off ok...\n");
            }
        }

        /****    Run Stop manual by button     ****/
        private void btn_scanSb_Click(object sender, EventArgs e)
        {
            scan_sb();
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
        int tim, tim1, tim2, next;
        int[] dt_hour, dt_minute, dt_second;
        DateTime date;

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (ModClient.Connected == true)
            {
                next++;
                urutan();
            }
        }
        void urutan()
        {
            if (next == 1)
            {
                sb1(); sb2(); richTextBox1.AppendText("Next SB.. " + next + "\n");
            }
            else if (next == 2)
            {
                sb3(); sb4(); richTextBox1.AppendText("Next SB.. " + next + "\n");
            }
            else if (next == 3)
            {
                sb5(); sb6(); richTextBox1.AppendText("Next SB.. " + next + "\n");
            }
            else if (next == 4)
            {
                sb7(); sb8(); richTextBox1.AppendText("Next SB.. " + next + "\n");
            }
            else if (next == 5)
            {
                sb9(); sb10(); richTextBox1.AppendText("Next SB.. " + next + "\n");
            }
            else if (next == 6)
            {
                sb11(); sb12(); richTextBox1.AppendText("Next SB.. " + next + "\n");
            }
            else if (next == 7)
            {
                sb13(); sb14(); richTextBox1.AppendText("Next SB.. " + next + "\n");
            }
            else if (next == 8)
            {
                sb15(); richTextBox1.AppendText("Next SB.. " + next + "\n");
            }
            else { next = 0; richTextBox1.AppendText("Next SB Reset.. " + next + "\n"); }
        }
        private void timer2_Tick(object sender, EventArgs e)
        {
            tim2++;
            label1.Text = tim2.ToString();
            date = DateTime.Now;
            //dt_hour[tim2] = date.Hour;
            label13.Text = date.ToString();
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            richTextBox2.AppendText("Timer 3 ticking...\n");

            for (byte i = 1; i < 31; i++)
            {
                //cek suhu pemasakan && run count pemasakan
                if (pv_val[i] >= float.Parse(start_pemasakan) && status_flag[i] == 0)
                {
                    count_pemasakan[i]++;
                    timer_pemasakan(count_pemasakan[i], i);

                    lbl_pemasakan1.Text = pemasakan_time;
                }
            }
        }

        void sb1()
        {
            try
            {
                id_sb = 1;

                readval_single();
                //cek_al1h();
                // mqrun_stop();
                pemasakan_off();


                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep1.Text = mq_resep[id_sb].ToString();
                lbl_durasi1.Text = durasi_time;

                lbl_sv1.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh1.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb1.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu1.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi1.Text = "Connected";
                btn_koneksi1.BackColor = Color.Green;
                btn_status1.Enabled = true;
                btn_status1.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //if stop
                {
                    btn_status1.Text = "Run";
                    btn_status1.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //if run
                {
                    btn_status1.Text = "Stop";
                    btn_status1.BackColor = Color.Red;
                }

                //cek out 1
                if (out1_flag[id_sb] == true)
                {
                    btn_out1.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out1.BackColor = Color.Red;
                }

                //cek alarm 1
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm1.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm1.BackColor = Color.Red;
                }

                //cek suhu pemasakan && run count pemasakan
                /*if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan1.Text = pemasakan_time;
                }*/
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                durasi(mq_durasi[id_sb], id_sb);
                //lbl_resep1.Text = mq_resep[id_sb].ToString();
                lbl_durasi3.Text = durasi_time;

                btn_koneksi1.Text = "Disconnected";
                btn_koneksi1.BackColor = Color.Red;
                btn_status1.Enabled = false;
                btn_status1.BackColor = Color.Gray;
            }
        }

        void sb2()
        {
            try
            {
                id_sb = 2;

                readval_single();
                //cek_al1h();
                //mqrun_stop();
                pemasakan_off();


                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep2.Text = mq_resep[id_sb].ToString();
                lbl_durasi2.Text = durasi_time;
                richTextBox3.AppendText("sb2.. " + id_sb + " Resep: " + mq_resep[id_sb] + " Durasi: " + mq_resep[id_sb] + "\n");


                lbl_sv2.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh2.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb2.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu2.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi2.Text = "Connected";
                btn_koneksi2.BackColor = Color.Green;
                btn_status2.Enabled = true;
                btn_status2.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //stop
                {
                    btn_status2.Text = "Run";
                    btn_status2.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //run
                {
                    btn_status2.Text = "Stop";
                    btn_status2.BackColor = Color.Red;
                }

                //cek out
                if (out1_flag[id_sb] == true)
                {
                    btn_out2.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out2.BackColor = Color.Red;
                }

                //cek alarm
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm2.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm2.BackColor = Color.Red;
                }

                //cek suhu pemasakan
                if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan2.Text = pemasakan_time;
                }
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                btn_koneksi2.Text = "Disconnected";
                btn_koneksi2.BackColor = Color.Red;
                btn_status2.Enabled = false;
                btn_status2.BackColor = Color.Gray;
            }
        }

        void sb3()
        {
            try
            {
                id_sb = 3;

                readval_single();
                //cek_al1h();
                //mqrun_stop();
                pemasakan_off();

                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep3.Text = mq_resep[id_sb].ToString();
                lbl_durasi3.Text = durasi_time;

                lbl_sv3.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh3.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb2.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu3.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi3.Text = "Connected";
                btn_koneksi3.BackColor = Color.Green;
                btn_status3.Enabled = true;
                btn_status3.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //stop
                {
                    btn_status3.Text = "Run";
                    btn_status3.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //run
                {
                    btn_status3.Text = "Stop";
                    btn_status3.BackColor = Color.Red;
                }

                //cek out
                if (out1_flag[id_sb] == true)
                {
                    btn_out3.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out3.BackColor = Color.Red;
                }

                //cek alarm
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm3.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm3.BackColor = Color.Red;
                }

                //cek suhu pemasakan
                if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan3.Text = pemasakan_time;
                }
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                durasi(mq_durasi[id_sb], id_sb);
                lbl_durasi3.Text = durasi_time;

                btn_koneksi3.Text = "Disconnected";
                btn_koneksi3.BackColor = Color.Red;
                btn_status3.Enabled = false;
                btn_status3.BackColor = Color.Gray;
            }
        }

        void sb4()
        {
            try
            {
                id_sb = 4;

                readval_single();
                //cek_al1h();
                mqrun_stop();
                pemasakan_off();


                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep4.Text = mq_resep[id_sb].ToString();
                lbl_durasi4.Text = durasi_time;

                lbl_sv4.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh4.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb2.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu4.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi4.Text = "Connected";
                btn_koneksi4.BackColor = Color.Green;
                btn_status4.Enabled = true;
                btn_status4.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //stop
                {
                    btn_status4.Text = "Run";
                    btn_status4.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //run
                {
                    btn_status4.Text = "Stop";
                    btn_status4.BackColor = Color.Red;
                }

                //cek out
                if (out1_flag[id_sb] == true)
                {
                    btn_out4.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out4.BackColor = Color.Red;
                }

                //cek alarm
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm4.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm4.BackColor = Color.Red;
                }

                //cek suhu pemasakan
                if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan4.Text = pemasakan_time;
                }
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                durasi(mq_durasi[id_sb], id_sb);
                lbl_durasi4.Text = durasi_time;

                btn_koneksi4.Text = "Disconnected";
                btn_koneksi4.BackColor = Color.Red;
                btn_status4.Enabled = false;
                btn_status4.BackColor = Color.Gray;
            }
        }

        void sb5()
        {
            try
            {
                id_sb = 5;

                readval_single();
                //cek_al1h();
                mqrun_stop();
                pemasakan_off();


                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep5.Text = mq_resep[id_sb].ToString();
                lbl_durasi5.Text = durasi_time;

                lbl_sv5.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh5.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb2.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu5.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi5.Text = "Connected";
                btn_koneksi5.BackColor = Color.Green;
                btn_status5.Enabled = true;
                btn_status5.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //stop
                {
                    btn_status5.Text = "Run";
                    btn_status5.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //run
                {
                    btn_status5.Text = "Stop";
                    btn_status5.BackColor = Color.Red;
                }

                //cek out
                if (out1_flag[id_sb] == true)
                {
                    btn_out5.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out5.BackColor = Color.Red;
                }

                //cek alarm
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm5.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm5.BackColor = Color.Red;
                }

                //cek suhu pemasakan
                if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan5.Text = pemasakan_time;
                }
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                durasi(mq_durasi[id_sb], id_sb);
                lbl_durasi5.Text = durasi_time;

                btn_koneksi5.Text = "Disconnected";
                btn_koneksi5.BackColor = Color.Red;
                btn_status5.Enabled = false;
                btn_status5.BackColor = Color.Gray;
            }
        }

        void sb6()
        {
            try
            {
                id_sb = 6;

                readval_single();
                //cek_al1h();
                mqrun_stop();
                pemasakan_off();


                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep6.Text = mq_resep[id_sb].ToString();
                lbl_durasi6.Text = durasi_time;

                lbl_sv6.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh6.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb2.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu6.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi6.Text = "Connected";
                btn_koneksi6.BackColor = Color.Green;
                btn_status6.Enabled = true;
                btn_status6.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //stop
                {
                    btn_status6.Text = "Run";
                    btn_status6.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //run
                {
                    btn_status6.Text = "Stop";
                    btn_status6.BackColor = Color.Red;
                }

                //cek out
                if (out1_flag[id_sb] == true)
                {
                    btn_out6.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out6.BackColor = Color.Red;
                }

                //cek alarm
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm6.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm6.BackColor = Color.Red;
                }

                //cek suhu pemasakan
                if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan6.Text = pemasakan_time;
                }
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                durasi(mq_durasi[id_sb], id_sb);
                lbl_durasi6.Text = durasi_time;

                btn_koneksi6.Text = "Disconnected";
                btn_koneksi6.BackColor = Color.Red;
                btn_status6.Enabled = false;
                btn_status6.BackColor = Color.Gray;
            }
        }

        void sb7()
        {
            try
            {
                id_sb = 7;

                readval_single();
                //cek_al1h();
                mqrun_stop();
                pemasakan_off();


                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep7.Text = mq_resep[id_sb].ToString();
                lbl_durasi7.Text = durasi_time;

                lbl_sv7.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh7.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb2.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu7.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi7.Text = "Connected";
                btn_koneksi7.BackColor = Color.Green;
                btn_status7.Enabled = true;
                btn_status7.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //stop
                {
                    btn_status7.Text = "Run";
                    btn_status7.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //run
                {
                    btn_status7.Text = "Stop";
                    btn_status7.BackColor = Color.Red;
                }

                //cek out
                if (out1_flag[id_sb] == true)
                {
                    btn_out7.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out7.BackColor = Color.Red;
                }

                //cek alarm
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm7.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm7.BackColor = Color.Red;
                }

                //cek suhu pemasakan
                if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan7.Text = pemasakan_time;
                }
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                durasi(mq_durasi[id_sb], id_sb);
                lbl_durasi7.Text = durasi_time;

                btn_koneksi7.Text = "Disconnected";
                btn_koneksi7.BackColor = Color.Red;
                btn_status7.Enabled = false;
                btn_status7.BackColor = Color.Gray;
            }
        }

        void sb8()
        {
            try
            {
                id_sb = 8;

                readval_single();
                //cek_al1h();
                mqrun_stop();
                pemasakan_off();


                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep8.Text = mq_resep[id_sb].ToString();
                lbl_durasi8.Text = durasi_time;

                lbl_sv8.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh8.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb2.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu8.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi8.Text = "Connected";
                btn_koneksi8.BackColor = Color.Green;
                btn_status8.Enabled = true;
                btn_status8.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //stop
                {
                    btn_status8.Text = "Run";
                    btn_status8.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //run
                {
                    btn_status8.Text = "Stop";
                    btn_status8.BackColor = Color.Red;
                }

                //cek out
                if (out1_flag[id_sb] == true)
                {
                    btn_out8.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out8.BackColor = Color.Red;
                }

                //cek alarm
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm8.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm8.BackColor = Color.Red;
                }

                //cek suhu pemasakan
                if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan8.Text = pemasakan_time;
                }
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                durasi(mq_durasi[id_sb], id_sb);
                lbl_durasi8.Text = durasi_time;

                btn_koneksi8.Text = "Disconnected";
                btn_koneksi8.BackColor = Color.Red;
                btn_status8.Enabled = false;
                btn_status8.BackColor = Color.Gray;
            }
        }

        void sb9()
        {
            try
            {
                id_sb = 9;

                readval_single();
                //cek_al1h();
                mqrun_stop();
                pemasakan_off();


                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep9.Text = mq_resep[id_sb].ToString();
                lbl_durasi9.Text = durasi_time;

                lbl_sv9.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh9.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb2.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu9.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi9.Text = "Connected";
                btn_koneksi9.BackColor = Color.Green;
                btn_status9.Enabled = true;
                btn_status9.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //stop
                {
                    btn_status9.Text = "Run";
                    btn_status9.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //run
                {
                    btn_status9.Text = "Stop";
                    btn_status9.BackColor = Color.Red;
                }

                //cek out
                if (out1_flag[id_sb] == true)
                {
                    btn_out9.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out9.BackColor = Color.Red;
                }

                //cek alarm
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm9.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm9.BackColor = Color.Red;
                }

                //cek suhu pemasakan
                if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan9.Text = pemasakan_time;
                }
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                durasi(mq_durasi[id_sb], id_sb);
                lbl_durasi9.Text = durasi_time;

                btn_koneksi9.Text = "Disconnected";
                btn_koneksi9.BackColor = Color.Red;
                btn_status9.Enabled = false;
                btn_status9.BackColor = Color.Gray;
            }
        }

        void sb10()
        {
            try
            {
                id_sb = 10;

                readval_single();
                //cek_al1h();
                mqrun_stop();
                pemasakan_off();


                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep10.Text = mq_resep[id_sb].ToString();
                lbl_durasi10.Text = durasi_time;

                lbl_sv10.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh10.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb2.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu10.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi10.Text = "Connected";
                btn_koneksi10.BackColor = Color.Green;
                btn_status10.Enabled = true;
                btn_status10.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //stop
                {
                    btn_status10.Text = "Run";
                    btn_status10.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //run
                {
                    btn_status10.Text = "Stop";
                    btn_status10.BackColor = Color.Red;
                }

                //cek out
                if (out1_flag[id_sb] == true)
                {
                    btn_out10.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out10.BackColor = Color.Red;
                }

                //cek alarm
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm10.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm10.BackColor = Color.Red;
                }

                //cek suhu pemasakan
                if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan10.Text = pemasakan_time;
                }
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                durasi(mq_durasi[id_sb], id_sb);
                lbl_durasi10.Text = durasi_time;

                btn_koneksi10.Text = "Disconnected";
                btn_koneksi10.BackColor = Color.Red;
                btn_status10.Enabled = false;
                btn_status10.BackColor = Color.Gray;
            }
        }

        void sb11()
        {
            try
            {
                id_sb = 11;

                readval_single();
                //cek_al1h();
                mqrun_stop();
                pemasakan_off();


                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep11.Text = mq_resep[id_sb].ToString();
                lbl_durasi11.Text = durasi_time;

                lbl_sv11.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh11.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb2.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu11.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi11.Text = "Connected";
                btn_koneksi11.BackColor = Color.Green;
                btn_status11.Enabled = true;
                btn_status11.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //stop
                {
                    btn_status11.Text = "Run";
                    btn_status11.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //run
                {
                    btn_status11.Text = "Stop";
                    btn_status11.BackColor = Color.Red;
                }

                //cek out
                if (out1_flag[id_sb] == true)
                {
                    btn_out11.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out11.BackColor = Color.Red;
                }

                //cek alarm
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm11.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm11.BackColor = Color.Red;
                }

                //cek suhu pemasakan
                if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan11.Text = pemasakan_time;
                }
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                durasi(mq_durasi[id_sb], id_sb);
                lbl_durasi11.Text = durasi_time;

                btn_koneksi11.Text = "Disconnected";
                btn_koneksi11.BackColor = Color.Red;
                btn_status11.Enabled = false;
                btn_status11.BackColor = Color.Gray;
            }
        }

        void sb12()
        {
            try
            {
                id_sb = 12;

                readval_single();
                //cek_al1h();
                mqrun_stop();
                pemasakan_off();


                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep12.Text = mq_resep[id_sb].ToString();
                lbl_durasi12.Text = durasi_time;

                lbl_sv12.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh12.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb2.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu12.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi12.Text = "Connected";
                btn_koneksi12.BackColor = Color.Green;
                btn_status12.Enabled = true;
                btn_status12.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //stop
                {
                    btn_status12.Text = "Run";
                    btn_status12.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //run
                {
                    btn_status12.Text = "Stop";
                    btn_status12.BackColor = Color.Red;
                }

                //cek out
                if (out1_flag[id_sb] == true)
                {
                    btn_out12.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out12.BackColor = Color.Red;
                }

                //cek alarm
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm12.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm12.BackColor = Color.Red;
                }

                //cek suhu pemasakan
                if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan12.Text = pemasakan_time;
                }
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                durasi(mq_durasi[id_sb], id_sb);
                lbl_durasi12.Text = durasi_time;

                btn_koneksi12.Text = "Disconnected";
                btn_koneksi12.BackColor = Color.Red;
                btn_status12.Enabled = false;
                btn_status12.BackColor = Color.Gray;
            }
        }
        void sb13()
        {
            try
            {
                id_sb = 13;

                readval_single();
                //cek_al1h();
                mqrun_stop();
                pemasakan_off();


                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep13.Text = mq_resep[id_sb].ToString();
                lbl_durasi13.Text = durasi_time;

                lbl_sv13.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh13.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb2.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu13.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi13.Text = "Connected";
                btn_koneksi13.BackColor = Color.Green;
                btn_status13.Enabled = true;
                btn_status13.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //stop
                {
                    btn_status13.Text = "Run";
                    btn_status13.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //run
                {
                    btn_status13.Text = "Stop";
                    btn_status13.BackColor = Color.Red;
                }

                //cek out
                if (out1_flag[id_sb] == true)
                {
                    btn_out13.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out13.BackColor = Color.Red;
                }

                //cek alarm
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm13.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm13.BackColor = Color.Red;
                }

                //cek suhu pemasakan
                if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan13.Text = pemasakan_time;
                }
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                durasi(mq_durasi[id_sb], id_sb);
                lbl_durasi13.Text = durasi_time;

                btn_koneksi13.Text = "Disconnected";
                btn_koneksi13.BackColor = Color.Red;
                btn_status13.Enabled = false;
                btn_status13.BackColor = Color.Gray;
            }
        }
        void sb14()
        {
            try
            {
                id_sb = 14;

                readval_single();
                //cek_al1h();
                mqrun_stop();
                pemasakan_off();


                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep14.Text = mq_resep[id_sb].ToString();
                lbl_durasi14.Text = durasi_time;

                lbl_sv14.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh14.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb2.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu14.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi14.Text = "Connected";
                btn_koneksi14.BackColor = Color.Green;
                btn_status14.Enabled = true;
                btn_status14.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //stop
                {
                    btn_status14.Text = "Run";
                    btn_status14.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //run
                {
                    btn_status14.Text = "Stop";
                    btn_status14.BackColor = Color.Red;
                }

                //cek out
                if (out1_flag[id_sb] == true)
                {
                    btn_out14.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out14.BackColor = Color.Red;
                }

                //cek alarm
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm14.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm14.BackColor = Color.Red;
                }

                //cek suhu pemasakan
                if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan14.Text = pemasakan_time;
                }
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                durasi(mq_durasi[id_sb], id_sb);
                lbl_durasi14.Text = durasi_time;

                btn_koneksi14.Text = "Disconnected";
                btn_koneksi14.BackColor = Color.Red;
                btn_status14.Enabled = false;
                btn_status14.BackColor = Color.Gray;
            }
        }
        void sb15()
        {
            try
            {
                id_sb = 15;

                readval_single();
                //cek_al1h();
                mqrun_stop();
                pemasakan_off();


                if (mq_resep[id_sb] == null)
                {
                    mq_resep[id_sb] = "-";
                }

                durasi(mq_durasi[id_sb], id_sb);
                lbl_resep15.Text = mq_resep[id_sb].ToString();
                lbl_durasi15.Text = durasi_time;

                lbl_sv15.Text = (sv_val[id_sb] / 10).ToString() + "." + (sv_val[id_sb] % 10).ToString();
                lbl_alh15.Text = (al1h_val[id_sb] / 10).ToString() + "." + (al1h_val[id_sb] % 10).ToString();
                //richTextBox3.AppendText("sb2.. " + id_sb + " Read AL1H" + al1h_val[id_sb] + "\n");
                lbl_suhu15.Text = (pv_val[id_sb] / 10).ToString() + "." + (pv_val[id_sb] % 10).ToString();

                //sb_aktif[id_sb] = 1;
                btn_koneksi15.Text = "Connected";
                btn_koneksi15.BackColor = Color.Green;
                btn_status15.Enabled = true;
                btn_status15.BackColor = Color.Green;

                //cek status run / stop
                if (status_flag[id_sb] == 1) //stop
                {
                    btn_status15.Text = "Run";
                    btn_status15.BackColor = Color.Green;
                }
                else if (status_flag[id_sb] == 0) //run
                {
                    btn_status15.Text = "Stop";
                    btn_status15.BackColor = Color.Red;
                }

                //cek out
                if (out1_flag[id_sb] == true)
                {
                    btn_out15.BackColor = Color.DarkGreen;
                }
                else if (out1_flag[id_sb] == false)
                {
                    btn_out15.BackColor = Color.Red;
                }

                //cek alarm
                if (alarm1_flag[id_sb] == true)
                {
                    btn_alarm15.BackColor = Color.DarkGreen;
                }
                else if (alarm1_flag[id_sb] == false)
                {
                    btn_alarm15.BackColor = Color.Red;
                }

                //cek suhu pemasakan
                if (pv_val[id_sb] >= float.Parse(start_pemasakan) && status_flag[id_sb] == 0)
                {
                    count_pemasakan[id_sb]++;
                    timer_pemasakan(count_pemasakan[id_sb], id_sb);

                    lbl_pemasakan15.Text = pemasakan_time;
                }
            }
            catch (TimeoutException)
            {
                sb_aktif[id_sb] = 0;

                durasi(mq_durasi[id_sb], id_sb);
                lbl_durasi15.Text = durasi_time;

                btn_koneksi15.Text = "Disconnected";
                btn_koneksi15.BackColor = Color.Red;
                btn_status15.Enabled = false;
                btn_status15.BackColor = Color.Gray;
            }
        }

    }
}
