using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using v2rayN.Mode;

namespace v2rayN.Handler
{
    class SpeedtestLiveHandler
    {
        private Config _config;
        private V2rayHandler _v2rayHandler;
        private List<int> _selecteds;
        Action<int, string> _updateFunc;
        private int pid = -1;

        public SpeedtestLiveHandler(ref Config config, ref V2rayHandler v2rayHandler, List<int> selecteds, Action<int, string> update)
        {
            _config = config;
            _v2rayHandler = v2rayHandler;
            _updateFunc = update;
            Task.Run(() => StartSpeedtestLiveThread());
        }

        private void StartSpeedtestLiveThread()
        {
            pid = -1;
            try
            {
                string msg = string.Empty;
                List<int> allServerIndexList = _config.vmess.Select((x, i) => i).ToList();

                pid = _v2rayHandler.LoadV2rayConfigString(_config, allServerIndexList);

                //Thread.Sleep(5000);
                
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            finally
            {
                // if (pid > 0) _v2rayHandler.V2rayStopPid(pid);
            }
        }

        public void RunSpeedtest(List<int> selecteds, Action testDone) {
            Task.Run(() =>
            {
                _selecteds = Utils.DeepCopy(selecteds);
                int httpPort = _config.GetLocalPort("speedtest");
                List<Task> tasks = new List<Task>();
                foreach (int itemIndex in _selecteds)
                {
                    if (_config.vmess[itemIndex].configType == (int)EConfigType.Custom)
                    {
                        continue;
                    }
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            WebProxy webProxy = new WebProxy(Global.Loopback, httpPort + itemIndex);
                            int responseTime = -1;
                            string status = GetRealPingTime(_config.speedPingTestUrl, webProxy, out responseTime);
                            string output = Utils.IsNullOrEmpty(status) ? FormatOut(responseTime, "ms") : FormatOut(status, "");
                            _updateFunc(itemIndex, output);
                        }
                        catch (Exception ex)
                        {
                            Utils.SaveLog(ex.Message, ex);
                        }
                    }));
                    //Thread.Sleep(100);
                }
                Task.WaitAll(tasks.ToArray());
                testDone();
            });
        }

        public void StopHandler()
        {
            if (pid != -1)
            {
                _v2rayHandler.V2rayStopPid(pid);
            }
        }

        private string GetRealPingTime(string url, WebProxy webProxy, out int responseTime)
        {
            string msg = string.Empty;
            responseTime = -1;
            try
            {
                HttpWebRequest myHttpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                myHttpWebRequest.Timeout = 5000;
                myHttpWebRequest.Proxy = webProxy;//new WebProxy(Global.Loopback, Global.httpPort);

                Stopwatch timer = new Stopwatch();
                timer.Start();

                HttpWebResponse myHttpWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse();
                if (myHttpWebResponse.StatusCode != HttpStatusCode.OK
                    && myHttpWebResponse.StatusCode != HttpStatusCode.NoContent)
                {
                    msg = myHttpWebResponse.StatusDescription;
                }
                timer.Stop();
                responseTime = timer.Elapsed.Milliseconds;

                myHttpWebResponse.Close();
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                msg = ex.Message;
            }
            return msg;
        }

        private string FormatOut(object time, string unit)
        {
            if (time.ToString().Equals("-1"))
            {
                return "Timeout";
            }
            return string.Format("{0}{1}", time, unit).PadLeft(6, ' ');
        }
    }
    
}
