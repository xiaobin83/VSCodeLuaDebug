using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Xml;

namespace GiderosPlayerRemote
{
    public enum LogType
    {
        PlayerOutput,
        Info,
        Warning
    }

    public class RemoteController
    {
        Socket soc;
        NetworkStream networkStream;
        int nextSeqId = 1;
        string projectFileName;
        Action<LogType, string> logger;

        public bool TryStart(
            string addr,
            int port,
            string gprojPath,
            Action<LogType, string> logger)
        {
            this.projectFileName = gprojPath;
            this.logger = logger;

            if (soc != null) { soc.Dispose(); }
            soc = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);
            try
            {
                soc.Connect(new IPEndPoint(IPAddress.Parse(addr), 15000));
            }
            catch (SocketException e)
            {
                if (e.ErrorCode == 10061) { return false; }
                else { throw; }
            }
            soc.SendBufferSize = 1024 * 1024;
            networkStream = new NetworkStream(soc);
            Play();
            return true;           
        }

        public void ReadLoop()
        {
            var reader = new GiderosMessageReader(soc);

            while (true)
            {
                ReceivedGiderosMessage msg = reader.TryTakeMessageFromBuffer();
                if (msg == null)
                {
                    if (reader.ReceiveMore() == 0) { return; }
                    continue;
                }

                byte msgType = msg.ReadByte();
                switch (msgType)
                {
                    case GiderosMessageType.Output:
                        HandleOutput(msg);
                        break;

                    case GiderosMessageType.FileList:
                        HandleFileList(msg);
                        break;

                    default:
                        logger(LogType.Warning, "unknown message:" + msgType);
                        break;
                }
            }
        }

        void HandleOutput(ReceivedGiderosMessage msg)
        {
            logger(LogType.PlayerOutput, msg.ReadString());
        }

        List<KeyValuePair<string, string>> fileList = new List<KeyValuePair<string, string>>();
        Dictionary<string, KeyValuePair<DateTime, byte[]>> md5 = new Dictionary<string, KeyValuePair<DateTime, byte[]>>();
        DependencyGraph dependencyGraph = new DependencyGraph();
        MD5 md5calculator = MD5.Create();

        void HandleFileList(ReceivedGiderosMessage msg)
        {
            // void Application::dataReceived(const QByteArray& d)
            // in gdrdeamon/application.cpp

            Queue<KeyValuePair<string, string>> fileQueue = new Queue<KeyValuePair<string, string>>();

            Dictionary<string, string> localFileMap = new Dictionary<string, string>();
            Dictionary<string, string> localFileMapReverse = new Dictionary<string, string>();
            foreach (var e in fileList)
            {
                localFileMap[e.Key] = e.Value;
                localFileMapReverse[e.Value] = e.Key;
            }

            Dictionary<string, KeyValuePair<int, byte[]>> remoteFileMap = new Dictionary<string, KeyValuePair<int, byte[]>>();
            while (msg.IsEOB() == false)
            {
                var file = msg.ReadString();
                if (file[0] == 'F')
                {
                    int age = msg.ReadInt();
                    byte[] md5 = msg.ReadBytes(16);
                    remoteFileMap[file.Substring(1)] =
                        new KeyValuePair<int, byte[]>(age, md5);
                }
                else if (file[0] == 'D')
                {
                }
            }

            // delete unused files
            foreach (var iter in remoteFileMap)
            {
                if (localFileMap.ContainsKey(iter.Key) == false)
                {
                    //printf("deleting: %s\n", qPrintable(iter->first));
                    NewMessage(GiderosMessageType.DeleteFile)
                        .AppendString(iter.Key)
                        .Send();
                    logger(LogType.Info, "delete " + iter.Key);
                }
            }

            // upload files
            string path = Path.GetDirectoryName(projectFileName);
            foreach (var iter in localFileMap)
            {
                KeyValuePair<int, byte[]>? riter = remoteFileMap.Find(iter.Key);

                string localfile = Path.Combine(path, iter.Value);

                bool send = false;
                if (riter.HasValue == false)
                {
                    //printf("always upload: %s\n", qPrintable(iter->first));
                    send = true;
                }
                else
                {
                    int localage = Util.FileAge(localfile);
                    int remoteage = riter.Value.Key;
                    byte[] localmd5 = md5[iter.Value].Value;
                    byte[] remotemd5 = riter.Value.Value;

                    if (localage < remoteage || localmd5.SequenceEqual(remotemd5) == false)
                    {
                        //printf("upload new file: %s\n", qPrintable(iter->first));
                        send = true;
                    }
                }

                if (send == true)
                {
                    fileQueue.Enqueue(new KeyValuePair<string, string>(iter.Key, localfile));
                }
                else
                {
                    //printf("don't upload: %s\n", qPrintable(iter->first));
                }
            }

            List<KeyValuePair<string, bool>> topologicalSort =
                dependencyGraph.TopologicalSort();

            List<string> luaFilesToPlay = topologicalSort
                .Where(x => x.Value == false)
                .Select(x => localFileMapReverse[x.Key])
                .ToList();

            //---------------------------------------------------------
            // 여기부터 void Application::timer()
            var cfolderSent = new HashSet<string>();
            while (fileQueue.Count > 0)
            {
                string s1 = fileQueue.Peek().Key;
                string s2 = fileQueue.Peek().Value;
                fileQueue.Dequeue();

                // create remote directories
                var dir = Path.GetDirectoryName(s1);
                if (cfolderSent.Contains(dir))
                {
                    // pass
                }
                else
                {
                    cfolderSent.Add(dir);
                    NewMessage(GiderosMessageType.CreateFolder)
                        .AppendString(dir)
                        .Send();
                    logger(LogType.Info, "cfolder " + dir);
                }

                string fileName = Path.Combine(path, s2);

                try
                {
                    byte[] bytes = File.ReadAllBytes(fileName);

                    NewMessage(GiderosMessageType.File)
                        .AppendString(s1)
                        .AppendByteArray(bytes)
                        .Send();
                    logger(LogType.Info, "send " + s1);
                }
                catch (FileNotFoundException)
                {
                    // md5 계산에서 이미 경고를 냈으므로
                    // 여기에선 무시한다
                }
            }

            //client_->sendProjectProperties(properties_);

            //-----------------------------------------------
            var playMsg = NewMessage(GiderosMessageType.Play);
            foreach (string f in luaFilesToPlay)
            {
                logger(LogType.Info, "play " + f);
                playMsg.AppendString(f);
            }
            playMsg.Send();
        }

        void Play()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(projectFileName);

            /*
            // read properties
            {
                QDomElement root = doc.documentElement();

                properties_.clear();

                QDomElement properties = root.firstChildElement("properties");

                // graphics options
                if (!properties.attribute("scaleMode").isEmpty())
                    properties_.scaleMode = properties.attribute("scaleMode").toInt();
                if (!properties.attribute("logicalWidth").isEmpty())
                    properties_.logicalWidth = properties.attribute("logicalWidth").toInt();
                if (!properties.attribute("logicalHeight").isEmpty())
                    properties_.logicalHeight = properties.attribute("logicalHeight").toInt();
                QDomElement imageScales = properties.firstChildElement("imageScales");
                for(QDomNode n = imageScales.firstChild(); !n.isNull(); n = n.nextSibling())
                {
                    QDomElement scale = n.toElement();
                    if(!scale.isNull())
                        properties_.imageScales.push_back(std::make_pair(scale.attribute("suffix"), scale.attribute("scale").toDouble()));
                }
                if (!properties.attribute("orientation").isEmpty())
                    properties_.orientation = properties.attribute("orientation").toInt();
                if (!properties.attribute("fps").isEmpty())
                    properties_.fps = properties.attribute("fps").toInt();

                // iOS options
                if (!properties.attribute("retinaDisplay").isEmpty())
                    properties_.retinaDisplay = properties.attribute("retinaDisplay").toInt();
                if (!properties.attribute("autorotation").isEmpty())
                    properties_.autorotation = properties.attribute("autorotation").toInt();

                // export options
                if (!properties.attribute("architecture").isEmpty())
                    properties_.architecture = properties.attribute("architecture").toInt();
                if (!properties.attribute("exportMode").isEmpty())
                    properties_.exportMode = properties.attribute("exportMode").toInt();
                if (!properties.attribute("iosDevice").isEmpty())
                    properties_.iosDevice = properties.attribute("iosDevice").toInt();
                if (!properties.attribute("packageName").isEmpty())
                    properties_.packageName = properties.attribute("packageName");
            }
            */

            // populate file list and dependency graph
            {
                fileList.Clear();
                dependencyGraph.Clear();
                var dependencies = new List<KeyValuePair<string, string>>();

                var stack = new Stack<XmlElement>();
                stack.Push(doc.DocumentElement);

                var dir = new List<string>();

                while (stack.Count > 0)
                {
                    XmlElement e = stack.Pop();

                    if (e == null)
                    {
                        dir.RemoveAt(dir.Count - 1);
                        continue;
                    }

                    string type = e.Name;

                    if (type == "file")
                    {
                        string fileName = e.HasAttribute("source")
                            ? e.GetAttribute("source")
                            : e.GetAttribute("file");
                        string name = Path.GetFileName(fileName);

                        string n = "";
                        foreach (string d in dir)
                            n += d + "/";
                        n += name;

                        fileList.Add(new KeyValuePair<string, string>(n, fileName));

                        if (fileName.ToLower().EndsWith(".lua"))
                        {
                            bool excludeFromExecution =
                                (e.HasAttribute("excludeFromExecution")) &&
                                (int.Parse(e.GetAttribute("excludeFromExecution")) != 0);
                            dependencyGraph.AddCode(fileName, excludeFromExecution);
                        }

                        continue;
                    }

                    if (type == "folder")
                    {
                        string name = e.GetAttribute("name");
                        dir.Add(name);

                        string n = "";
                        foreach (string d in dir)
                            n += d + "/";

                        stack.Push(null);
                    }

                    if (type == "dependency")
                    {
                        string from = e.GetAttribute("from");
                        string to = e.GetAttribute("to");

                        dependencies.Add(new KeyValuePair<string, string>(from, to));
                    }

                    var childNodes = e.ChildNodes;
                    foreach (var c in childNodes)
                        stack.Push((XmlElement)c);
                }

                foreach (var d in dependencies)
                    dependencyGraph.AddDependency(d.Key, d.Value);
            }

            UpdateMD5();

            NewMessage(GiderosMessageType.Stop)
                .Send();

            NewMessage(GiderosMessageType.SetProjectName)
                .AppendString(Path.GetFileNameWithoutExtension(projectFileName))
                .Send();

            NewMessage(GiderosMessageType.SendFileList)
                .Send();
        }

        void UpdateMD5()
        {
            //var begin = DateTime.Now;

            // .tmp/~.md5 파일 포맷이 QDataStream에 의존하기 때문에
            // 역공학하기 귀찮아서 .md5 캐싱 구현 스킵함.
            // 요즘 PC에서 md5 전체 재계산하는거 얼마나 느린지 모르겠는데
            // 일단 캐시 없이 구현해서 듀얼 정도 스케일에서 계산 돌려보고 느리면 캐싱하는 걸로..
            string path = Path.GetDirectoryName(projectFileName);

            foreach (var f in fileList)
            {
                var filename = f.Value;
                var absfilename = Path.Combine(path, filename);
                DateTime mtime = File.GetLastWriteTime(absfilename);

                //if (iter == md5_.end() || mtime != iter.value().first)
                {
                    md5[filename] = new KeyValuePair<DateTime, byte[]>(
                        mtime,
                        MD5FromFile(absfilename));
                }
            }

            //logger("md5 time: " + (DateTime.Now - begin).TotalSeconds.ToString());
        }

        byte[] MD5FromFile(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    return md5calculator.ComputeHash(stream);
                }
            }
            catch (FileNotFoundException)
            {
                logger(LogType.Warning, "Fild not found: " + path);
                return new byte[16];
            }
        }

        GiderosMessageToSend NewMessage(byte ty)
        {
            var rv = new GiderosMessageToSend(nextSeqId++, networkStream);
            rv.AppendByte(ty);
            return rv;
        }
    }
}
