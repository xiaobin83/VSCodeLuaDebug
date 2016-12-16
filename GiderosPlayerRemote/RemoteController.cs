using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace GiderosPlayerRemote
{
    class RemoteController
    {
        Socket soc;
        NetworkStream networkStream;
        int nextSeqId = 1;
        string projectFileName;

        public void Run(
            string addr,
            int port,
            string gprojPath)
        {
            this.projectFileName = gprojPath;

            soc = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);
            soc.Connect(new IPEndPoint(IPAddress.Parse(addr), 15000));
            networkStream = new NetworkStream(soc);

            Play();
            ReadLoop(soc);
        }

        void ReadLoop(Socket from)
        {
            var reader = new GiderosMessageReader(from);

            while (true)
            {
                ReceivedGiderosMessage msg = reader.TryTakeMessageFromBuffer();
                if (msg == null)
                {
                    reader.ReceiveMore();
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
                        Console.WriteLine("unknown message:" + msgType);
                        break;
                }
            }
        }

        void HandleOutput(ReceivedGiderosMessage msg)
        {
            Console.Write(msg.ReadString());
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
                    Console.WriteLine("delete " + iter.Key);
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
            while (fileQueue.Count > 0)
            {
                string s1 = fileQueue.Peek().Key;
                string s2 = fileQueue.Peek().Value;
                fileQueue.Dequeue();

                // create remote directories
                NewMessage(GiderosMessageType.CreateFolder)
                    .AppendString(Path.GetDirectoryName(s1))
                    .Send();
                Console.WriteLine("cfolder " + Path.GetDirectoryName(s1));

                string fileName = Path.Combine(path, s2);
                byte[] bytes = File.ReadAllBytes(fileName);

                NewMessage(GiderosMessageType.File)
                    .AppendString(s1)
                    .AppendByteArray(bytes)
                    .Send();
                Console.WriteLine("send " + s1);
            }

            //client_->sendProjectProperties(properties_);

            //-----------------------------------------------
            var playMsg = NewMessage(GiderosMessageType.Play);
            foreach (string f in luaFilesToPlay)
            {
                Console.WriteLine("play " + f);
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
                    using (var stream = File.OpenRead(absfilename))
                    {
                        md5[filename] = new KeyValuePair<DateTime, byte[]>(
                            mtime,
                            md5calculator.ComputeHash(stream));
                    }
                }
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
