using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CASAS_Kinect
{
    class SerializerClass
    {

        public string action { get; set; }
        public string secret { get; set; }

        public Data data {get; set;}
        public string site {get; set;}
        public string channel {get; set;}

    }
    
    class Data
    {
        public string by { get; set; }
        public string category { get; set; }
        public string message { get; set; }
        public string sensor_type { get; set; }
        public string package_type { get; set; }
        public string serial { get; set; }
        public string target { get; set; }
        public string epoch { get; set; }
        public string uuid { get; set; }

        public Data(string _by, string _cat, string _sen, string _pack, string _tar, string _epc, string _id)
        {
            by = _by;
            category = _cat;
            sensor_type = _sen;
            package_type = _pack;
            
            target = _tar;
            epoch = _epc;
            uuid = _id;
        }
    }

    class rabbitMessage
    {

        public string routingKey {get; set;}
        public string message { get; set; }

        public rabbitMessage(string _rkey, string _mes)
        {

            routingKey = _rkey;
            message = _mes;

        }

    }

    class JointSerializer
    {

        public string id;
        public string joint;
        public string X;
        public string Y;
        public string Z;
        public string Sx;
        public string Sy;
        public string Sz;
        public string BO;
        public string State;

        public JointSerializer(string jn, int jT, double x, double y, double z, string state, string bo, double sx, double sy, double sz)
        {

            joint = jn;
            id = jT.ToString();
            X = x.ToString();
            Y = y.ToString();
            Z = z.ToString();
            Sx = sx.ToString();
            Sy = sy.ToString();
            Sz = sz.ToString();
            BO = bo;
            State = state;

        }

    }


}
