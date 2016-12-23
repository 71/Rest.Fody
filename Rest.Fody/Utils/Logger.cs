using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rest.Fody
{
    internal sealed class Logger
    {
        public static Logger Instance;

        public string Step { get; private set; }
        public IReadOnlyList<string> DoneSteps { get { return done_steps.AsReadOnly(); } }

        private List<string> done_steps;
        private Stack<string> layers;

        private Action<string> logsInfo;
        private Action<string> logsImportant;

        private object lockObj = Guid.NewGuid();

        public Logger(Action<string> info, Action<string> important)
        {
            Instance = this;

            layers = new Stack<string>();
            done_steps = new List<string>();

            logsImportant = important;
#if DEBUG
            logsInfo = important;
#else
            logsInfo = info;
#endif
        }

        private void InternalLog(string s, bool important)
        {
            lock (lockObj)
            {
                string log = layers.Count > 0
                    ? Enumerable.Range(0, layers.Count).Aggregate("", (str, c) => str += "  ") + $"[{String.Join(" > ", layers.Reverse())}] {s}"
                    : s;

                if (important) logsImportant(log);
                else logsInfo(log);
            }
        }

        /// <summary>
        /// Enter a region. In a region, all logs are prexifed by its name in brackets.
        /// </summary>
        public void Region(string name, Action action)
        {
            lock (lockObj)
            {
                layers.Push(name);
                action();
                layers.Pop();
            }
        }

        /// <summary>
        /// Enter a region. In a region, all logs are prexifed by its name in brackets.
        /// </summary>
        public T Region<T>(string step, Func<T> action)
        {

            lock (lockObj)
            {
                layers.Push(step);
                T res = action();
                layers.Pop();
                return res;
            }
        }

        /// <summary>
        /// Log a non-important message: "name is null" or "name isn't null".
        /// </summary>
        public void LogNull(string name, object o)
        {
            Log($"{name} is{(o == null ? " null" : "n't null")}");
        }

        /// <summary>
        /// Log a non-important step.
        /// </summary>
        public void Log(string step)
        {
            Log(step, false);
        }

        /// <summary>
        /// Log a step.
        /// </summary>
        public void Log(string step, bool important)
        {
            if (Step != null)
                done_steps.Add(Step);

            InternalLog(step, important);
            Step = step;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("DONE:");

            foreach (string s in done_steps)
                sb.AppendLine(s);

            sb.AppendLine();
            sb.AppendLine("CURRENT STACK:");

            string[] arr = layers.ToArray();
            for (int i = 0; i < arr.Length; i++)
            {
                foreach (int _ in Enumerable.Range(0, i))
                    sb.Append(" ");

                sb.AppendLine(arr[i]);
            }

            return sb.ToString();
        }
    }
}
