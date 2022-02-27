namespace Equipo4EdgeDrone
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Newtonsoft.Json;

    class Program
    {
        const int timeDelay = 1;
        static double x, y, z, vx, vy, vz = 0;
        static Random acc_gen = new Random();
        static double boundary = 10;
        static double acc_max = 1;
        static double acc_min = -1;

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        static Message GenerateUpdatedPositionMessage()
        {
            double ax = acc_min + acc_gen.NextDouble()*(acc_max-acc_min); //acceleration generation
            double ay = acc_min + acc_gen.NextDouble()*(acc_max-acc_min);
            double az = acc_min + acc_gen.NextDouble()*(acc_max-acc_min);

            vx = vx + ax*timeDelay; //speed update
            vy = vy + ay*timeDelay;
            vz = vz + az*timeDelay;

            x = Math.Min(Math.Max(-boundary, x + vx), boundary); //position update with boundary check
            y = Math.Min(Math.Max(-boundary, y + vy), boundary);
            z = Math.Max(z + vz, 0);

            //build message and serialization
            var positionDataPoint = new
            {
                x = x,
                y = y,
                z = z,
                speed = Math.Sqrt(vx*vx + vy*vy + vz*vz)
            };
            var messageString = JsonConvert.SerializeObject(positionDataPoint);
            var message = new Message(Encoding.ASCII.GetBytes(messageString));
            //Log data
            Console.WriteLine("{0} - Position Report: {1}", DateTime.Now, messageString);
            return message;
        }

        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("Drone Position Module - IoT Hub module client initialized.");

            for(int i=0; i<10000; i++){
                // Register callback to be called when a message is received by the module
                await ioTHubModuleClient.SendEventAsync("dronOutput",GenerateUpdatedPositionMessage());
                Thread.Sleep(5000);
            }
        }
    }
}