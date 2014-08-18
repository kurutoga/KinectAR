using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using RabbitMQ.Client;
using Microsoft.Kinect;
using System.Threading;
using System.Drawing;
using System.Drawing.Imaging;
using System.Timers;
using System.IO;

namespace CASAS_Kinect
{
    class Program
    {

        //core workers.
        JavaScriptSerializer jsserializer;
        KinectSensorCollection sensors;

        //to lock threads and keep track of important blocks.
        public static readonly object _locker = new object();
        public static readonly object _locker2 = new object();
        public static readonly object _locker3 = new object();
        public static readonly object _locker4 = new object();
        public static readonly object _locker5 = new object();

        //primary config handler
        protected Dictionary<string, string> appConfig = new Dictionary<string,string>();

        //rabbitmq factory.
        protected IConnection connection;
        protected ConnectionFactory factory = new ConnectionFactory();

        //timers for color and skeletal streams.
        protected Dictionary<KinectSensor, System.Timers.Timer> skeletalTimer = new Dictionary<KinectSensor, System.Timers.Timer>();
        protected Dictionary<KinectSensor, bool> halfQ = new Dictionary<KinectSensor, bool>();
        protected Dictionary<KinectSensor, bool> halfC = new Dictionary<KinectSensor, bool>();
        protected ColorImageFormat cif;

        //start pointer points here. 
        static void Main(string[] args)
        {
            //lets start our kinect app.
            Program kinectCore = new Program();


            //never surrender.
            while (true)
            {
                System.Threading.Thread.Sleep(1500);
            }

        }

        
         // Input: string filename.
         // Output: void
         // Info: reads config file and stores in appConfig dictionary.
        public void readConfig(string fs)
        {
            using (StreamReader sr = new StreamReader(fs))
            {
                string line;
                Dictionary<string, string> dict = new Dictionary<string, string>();
                
                //linq
                while ((line = sr.ReadLine()) != null)
                {
                    dict = File.ReadAllLines(fs)
                                   .Select(l => l.Split(new[] { '=' }))
                                   .ToDictionary(s => s[0].Trim(), s => s[1].Trim());
                }

                appConfig["target"] = dict["target"];
                appConfig["kinect1"] = dict["kinect1"];
                appConfig["kinect2"] = dict["kinect2"];
                appConfig["kinect3"] = dict["kinect3"];
                appConfig["kinect1_angle"] = dict["kinect1_angle"];
                appConfig["kinect2_angle"] = dict["kinect2_angle"];
                appConfig["kinect3_angle"] = dict["kinect3_angle"];
                appConfig["kinect1_usb"] = dict["kinect1_usb"];
                appConfig["kinect2_usb"] = dict["kinect2_usb"];
                appConfig["kinect3_usb"] = dict["kinect3_usb"];
                appConfig["tbname"] = dict["tbname"];
                appConfig["saveFile"] = dict["saveFile"];
                appConfig["event_id"] = dict["event_id"];
                appConfig["tcpserver"] = dict["tcpserver"];
                appConfig["tcpport"] = dict["tcpport"];
                appConfig["tcpsend"] = dict["tcpsend"];
                appConfig["rabbitqueue"] = dict["rabbitqueue"];
                appConfig["rabbitkey"] = dict["rabbitkey"];
                appConfig["rabbithost"] = dict["rabbithost"];
                appConfig["rabbituser"] = dict["rabbituser"];
                appConfig["rabbitpass"] = dict["rabbitpass"];
                appConfig["rabbitport"] = dict["rabbitport"];
                appConfig["rabbitvhost"] = dict["rabbitvhost"];
                appConfig["kinect1color"] = dict["kinect1color"];
                appConfig["kinect2color"] = dict["kinect2color"];
                appConfig["kinect3color"] = dict["kinect3color"];

                //do close.
                sr.Close();
            }

        }

        //to update config file.
        public void saveConfig(string fs)
        {
            lock (_locker)
            {

                using (StreamWriter sw = new StreamWriter(fs))
                {
                    foreach (var d in appConfig)
                    {
                        sw.WriteLine(d.Key + "=" + d.Value);
                    }
                    sw.Close();
                }
            }
        }


        //constructor.
        public Program() {

            readConfig(@"C:\CASAS_Kinect\app.conf");

            factory.HostName = appConfig["rabbithost"];
            factory.UserName = appConfig["rabbituser"];
            factory.Password = appConfig["rabbitpass"];
            factory.Port = Convert.ToInt32(appConfig["rabbitport"]);
            factory.VirtualHost = appConfig["rabbitvhost"];

            connection = factory.CreateConnection();

            System.Threading.Thread.Sleep(2000);

            sensors = KinectSensor.KinectSensors;
            sensors.StatusChanged += sensors_StatusChanged;

            jsserializer = new JavaScriptSerializer();

            foreach (KinectSensor ks in sensors)
            {
                try
                {
                    if (ks.Status == KinectStatus.Connected)
                    {
                        StartSensor(ks);
                    }
                }
                catch (Exception ex)
                {

                    saveLocal("\nError Encountered: "+ ex.Message);

                }

            }
        
        }

        //deconstructor. remember cpts121?
        ~Program()
        {

        }

        public void saveLocal(object message)
        {


            lock (_locker2)
            {
                message = Convert.ChangeType(message, typeof(string));
                FileStream local = new FileStream((@"C:\CASAS_Kinect\data\data" + appConfig["saveFile"] + ".json").ToString(), FileMode.Append, FileAccess.Write, FileShare.Write);
                int temp;
                while (local.Length > 100000000)
                {
                    Int32.TryParse(appConfig["saveFile"], out temp);
                    temp += 1;
                    appConfig["saveFile"] = temp.ToString();

                    saveConfig(@"C:\CASAS_Kinect\app.conf");

                    local.Close();
                    local = new FileStream(("C:/CASAS_Kinect/data" + appConfig["saveFile"] + ".json").ToString(), FileMode.Append, FileAccess.Write, FileShare.Write);
                }

                var sw = new StreamWriter(local);
                sw.WriteLine(message.ToString());
                if (local.Length < 100000000)
                {
                    sw.WriteLine(", ");
                }
                sw.Close();

            }
        }

        void StopSensor(KinectSensor ks)
        {

            // check if it's not stopped already.
            if (ks == null)
            {
                return;

            }

            try
            {
               
                //if not stopped. close all cameras and inputs. [NOTE: Add audio streams]
                if (ks.DepthStream.IsEnabled)
                {
                    ks.DepthStream.Disable();
                }
                if (ks.SkeletonStream.IsEnabled)
                {
                    ks.SkeletonStream.Disable();
                }
                if (ks.ColorStream.IsEnabled)
                {
                    ks.ColorStream.Disable();
                }

              

                ks.AllFramesReady -= ks_allFramesReady;
                // unsubscribe from the event.
                //ks.SkeletonFrameReady -= ks_SkeletonFrameReady;
                //ks.ColorFrameReady -= ks_ColorFrameReady;

                ks.Stop();
            }
            catch (Exception ex)
            {
                saveLocal(ex.Message); //error message save.
            }
        }
        void StartSensor(KinectSensor ks)
        {
            //Debugging [NOTE: Comment it out before testing]
            Console.WriteLine("Starting Sensor {0}", ks.DeviceConnectionId);


            for (int i = 1; i < 4; i++)
            {
                if (appConfig["kinect" + i + "_usb"] == ks.DeviceConnectionId)
                {

                    if (appConfig["kinect" + i + "color"] == "yes")
                    {
                        cif = ColorImageFormat.RgbResolution640x480Fps30;
                        break;
                    }
                    else
                    {
                        cif = ColorImageFormat.InfraredResolution640x480Fps30;
                        break;
                    }
                }
                else
                {
                    cif = ColorImageFormat.RgbResolution640x480Fps30;
                }
            }

            
            //smoothing parameters best for skeletal tracking.
            TransformSmoothParameters smoothingParam = new TransformSmoothParameters();
            {
                smoothingParam.Smoothing = 0.7f;
                smoothingParam.Correction = 0.3f;
                smoothingParam.Prediction = 1.0f;
                smoothingParam.JitterRadius = 1.0f;
                smoothingParam.MaxDeviationRadius = 1.0f;
            };


            try
            {
                //Start up the kinect to start streaming data
                ks.Start();


                //30fps 640x480 stream
                //Enable the depth stream [NOTE: change fps]
                ks.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                //Select smoothing parameters and pass into enable method
                //if you want smoothing.

                ks.SkeletonStream.Enable(smoothingParam);
                ks.ColorStream.Enable(cif);

                skeletalTimer[ks] = new System.Timers.Timer(500);
                skeletalTimer[ks].AutoReset = true;
                skeletalTimer[ks].Elapsed += (sender, e) => skeletalTimerEvent(sender, e, ks);
                skeletalTimer[ks].Enabled = true;

                halfC[ks] = true;
                halfQ[ks] = true;

                ks.AllFramesReady += ks_allFramesReady;

                
            }
            catch (Exception ex)
            {
                //SendTCPMessage(jsserializer.Serialize(new Message(msgIndex++, ex)));
                Console.WriteLine("Error configuring Kinect- Error: {0}", ex);
                saveLocal(ex.Message);
            }
        }
        private void skeletalTimerEvent(Object source, ElapsedEventArgs e, KinectSensor ks)
        {

            foreach (var timer in skeletalTimer)
            {
                if (timer.Key == ks)
                {
                    lock (_locker4)
                    {
                        halfC[timer.Key] = true;
                        halfQ[timer.Key] = true;
                    }

                }
            }

        }

        void ks_allFramesReady(object sender, AllFramesReadyEventArgs e)
        {

            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            string epoch = t.TotalSeconds.ToString();
            var tempKS = (KinectSensor)sender;
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {

                    byte[] colorData;
                    //Using standard SDK
                    colorData = new byte[colorFrame.PixelDataLength];
                    colorFrame.CopyPixelDataTo(colorData);
                    Bitmap bmap = new Bitmap(colorFrame.Width, colorFrame.Height, PixelFormat.Format32bppRgb);
                    BitmapData bmapdata = bmap.LockBits(
                    new Rectangle(0, 0, colorFrame.Width, colorFrame.Height),
                     ImageLockMode.WriteOnly,
                     bmap.PixelFormat);
                    IntPtr ptr = bmapdata.Scan0;
                    Marshal.Copy(colorData, 0, ptr, colorFrame.PixelDataLength);
                    bmap.UnlockBits(bmapdata);

                    lock (_locker4)
                    {
                        if (halfQ[tempKS])
                        {
                            //            RGBObject obj = new RGBObject(bmap, epoch);
                            //          ThreadPool.QueueUserWorkItem(new WaitCallback(RabbitMQ), (object)obj);

                        //    System.IO.MemoryStream stream = new System.IO.MemoryStream();
                        //    bmap.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
                       //     byte[] imageBytes = stream.ToArray();
                            string base64String = GetString(colorData) ;
                            SerializerClass cData = new SerializerClass();
                            cData = getColorData(base64String, tempKS, epoch);
                            string message = jsserializer.Serialize(cData); //move to separate thread?
                            rabbitMessage rMsg = new rabbitMessage("color", message);
                            ThreadPool.QueueUserWorkItem(new WaitCallback(sendtoRabbitMQ), (object)rMsg);
                            halfQ[tempKS] = false;
                        }
                    }

                }
            }

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                //Create buffer to store skeletons
                Skeleton[] skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];

                //Write skeleton data into buffer
                skeletonFrame.CopySkeletonDataTo(skeletons);

                //Loop into each skeleton
                foreach (Skeleton skel in skeletons)
                {
                    switch (skel.TrackingState)
                    {
                        case SkeletonTrackingState.NotTracked:
                            //No skeleton data, no need to do a thing
                            continue;
                        default:
                            //Serialize position data to a JSON string for sending
                            SerializerClass dataClass = new SerializerClass();
                            dataClass = getSkelData(skel, tempKS, epoch);
                            dataClass.action = "rawevents";
                            dataClass.site = "kyoto";
                            string message = jsserializer.Serialize(dataClass); //move to separate thread?
                            rabbitMessage rMsg = new rabbitMessage("skeletal", message);
                            //SendTCPMessage(message);
                            ThreadPool.QueueUserWorkItem(new WaitCallback(saveLocal), (object)message);
                            try
                            {
                                lock (_locker4)
                                {
                                    if (halfC[tempKS])
                                    {
                                        ThreadPool.QueueUserWorkItem(new WaitCallback(sendtoRabbitMQ), (object)rMsg);
                                        halfC[tempKS] = false;
                                    }
                                }
                            }
                            catch (Exception ex) {

                                saveLocal(ex.Message);
                            
                            }
                            //sendtoRabbitMQ(message);

                            continue;

                    }
                }
            }

        }

        static string GetString(byte[] bytes)
        {
            char[] chars = new char[bytes.Length / sizeof(char)];
            System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }

        SerializerClass getSkelData(Skeleton skel, KinectSensor ks, string epoch)
        {
            SerializerClass dataSerializer = new SerializerClass();
            dataSerializer.data = new Data("kinect", "entity", "Skeletal", "Kinect", "SK01", epoch, Guid.NewGuid().ToString());
          
            for (int i = 1; i < 4; i++)
            {
                if (ks.DeviceConnectionId == appConfig["kinect" + i + "_usb"])
                {
                    dataSerializer.data.serial = appConfig["kinect" + i];
                }

            }
            if (skel.TrackingState != SkeletonTrackingState.PositionOnly)
            {
                List<Joint> jointData = new List<Joint>();
                jointData.Add(skel.Joints[JointType.AnkleLeft]);
                jointData.Add(skel.Joints[JointType.AnkleRight]);
                jointData.Add(skel.Joints[JointType.ElbowLeft]);
                jointData.Add(skel.Joints[JointType.ElbowRight]);
                jointData.Add(skel.Joints[JointType.FootLeft]);
                jointData.Add(skel.Joints[JointType.FootRight]);
                jointData.Add(skel.Joints[JointType.HandLeft]);
                jointData.Add(skel.Joints[JointType.HandRight]);
                jointData.Add(skel.Joints[JointType.Head]);
                jointData.Add(skel.Joints[JointType.HipCenter]);
                jointData.Add(skel.Joints[JointType.HipLeft]);
                jointData.Add(skel.Joints[JointType.HipRight]);
                jointData.Add(skel.Joints[JointType.KneeLeft]);
                jointData.Add(skel.Joints[JointType.KneeRight]);
                jointData.Add(skel.Joints[JointType.ShoulderCenter]);
                jointData.Add(skel.Joints[JointType.ShoulderLeft]);
                jointData.Add(skel.Joints[JointType.ShoulderRight]);
                jointData.Add(skel.Joints[JointType.Spine]);
                jointData.Add(skel.Joints[JointType.WristLeft]);
                jointData.Add(skel.Joints[JointType.WristRight]);

                dataSerializer.data.message = "[";
                foreach (Joint joint in jointData)
                {
                    SkeletonPoint sp = joint.Position;
                    DepthImagePoint depthPoint = ks.CoordinateMapper.MapSkeletonPointToDepthPoint(sp, ks.DepthStream.Format);
                    Vector4 mt4 = skel.BoneOrientations[joint.JointType].AbsoluteRotation.Quaternion;
                    string bo = mt4.W.ToString() + "##" + mt4.X.ToString() + "##" + mt4.Y.ToString() + "##" + mt4.Z.ToString();
                    JointSerializer js = new JointSerializer(joint.JointType.ToString(), skel.TrackingId, joint.Position.X, joint.Position.Y, joint.Position.Z, joint.TrackingState.ToString(), bo, depthPoint.X, depthPoint.Y, depthPoint.Depth);
                    dataSerializer.data.message += jsserializer.Serialize(js) + ",";

                }
                dataSerializer.data.message = dataSerializer.data.message.Substring(0, dataSerializer.data.message.Length - 1);
                dataSerializer.data.message += "]";
            }
            else
            {
                dataSerializer.data.message = "[";
                dataSerializer.data.message += skel.TrackingId.ToString()+"##"+ "X:" + skel.Position.X.ToString() + "##Y:" + skel.Position.Y.ToString() + "##Z:" + skel.Position.Z.ToString()+"]";
            }

            return dataSerializer;

        }

        SerializerClass getColorData(string b64, KinectSensor ks, string epoch)
        {
            SerializerClass dataSerializer = new SerializerClass();
            dataSerializer.data = new Data("kinect", "entity", "Color", "Kinect", "RGB02", epoch, Guid.NewGuid().ToString());

            for (int i = 1; i < 4; i++)
            {
                if (ks.UniqueKinectId == appConfig["kinect" + i + "_usb"])
                {
                    dataSerializer.data.serial = appConfig["kinect" + i];
                }

            }

            dataSerializer.data.message = b64;

            return dataSerializer;

        }
        void sensors_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            String msg = String.Format("A kinect sensor has changed its status to: {0}", e.Status.ToString());
            Console.WriteLine(msg);

            //Start up the sensor if it was just plugged in
            if (e.Status == KinectStatus.Connected) StartSensor(e.Sensor);
            if (e.Status == KinectStatus.Disconnected) StopSensor(e.Sensor);
        }

        public void sendtoRabbitMQ(object message)
        {

            rabbitMessage rMsg = (rabbitMessage)message;
            Console.Write(rMsg.routingKey + "Data :");

            try
            {

                using (var channel = connection.CreateModel())
                {
                    //channel.QueueDeclare("CASAS_QUEUE", true, false, false, null);
                    //channel.ExchangeDeclare(EXCHANGE_NAME, ExchangeType.Fanout, true, true, null);

                    var message2 = GetMessage(new string[] { rMsg.message });
                    var body = Encoding.UTF8.GetBytes(message2);

                    var properties = channel.CreateBasicProperties();
                    properties.SetPersistent(true);

                    channel.BasicPublish("kinect", rMsg.routingKey, properties, body);
                    Console.WriteLine(" [x] Sent at {0}:{1}", DateTime.Now.ToLongTimeString(), body.Length.ToString());


                }
            }
            catch (Exception ex)
            {
                saveLocal(ex.Message);
            }
        }
        private static string GetMessage(string[] args)
        {
            return ((args.Length > 0) ? string.Join(" ", args) : "Blank Message!");
        }

    }
}
