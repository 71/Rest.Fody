using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rest.Fody
{
    internal sealed class Logger
    {
        private List<string> done_steps;
        private Stack<string> layers;

        private Action<string> logsInfo;
        private Action<string> logsImportant;

        private object lockObj;


        public IReadOnlyList<string> DoneSteps { get { return done_steps.AsReadOnly(); } }
        public string Step { get; private set; }

        public Logger(Action<string> info, Action<string> important)
        {
            layers = new Stack<string>();
            done_steps = new List<string>();
            lockObj = Guid.NewGuid();

            logsImportant = important;

#if DEBUG
            logsInfo = important;
#else
            logsInfo = info;
#endif
        }

        private void InternalLog(string s, bool important)
        {
            string log = layers.Count > 0
                ? Enumerable.Range(0, layers.Count).Aggregate("", (str, c) => str += "  ") + $"[{String.Join(" > ", layers.Reverse())}] {s}"
                : s;

            if (important) logsImportant(log);
            else logsInfo(log);
        }

        /// <summary>
        /// Start logging a big step.
        /// All logs executed while running the given <see cref="Action"/>
        /// will be marked with a [step] tag.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="action"></param>
        public void Log(string step, Action action)
        {
            lock (lockObj)
                layers.Push(step);

            action();

            lock (lockObj)
                layers.Pop();
        }

        public void LogNull(string name, object o)
        {
            Log($"{name} is{(o == null ? " null" : "n't null")}");
        }

        /// <summary>
        /// Start logging a big step.
        /// All logs executed while running the given <see cref="Action"/>
        /// will be marked with a [step] tag.
        /// </summary>
        public T Log<T>(string step, Func<T> action)
        {
            lock (lockObj)
                layers.Push(step);

            T res = action();

            lock (lockObj)
                layers.Pop();

            return res;
        }

        public void Log(string step)
        {
            Log(step, false);
        }

        public void Log(string step, bool important)
        {
            if (Step != null)
                done_steps.Add(Step);

            InternalLog(step, important);

            lock (lockObj)
                Step = step;
        }

        public void LogAll(IEnumerable<string> toLog)
        {
            foreach (string s in toLog ?? new string[0])
                Log(s, false);
        }

        public void LogAll(string msg, IEnumerable<string> toLog)
        {
            Log(msg, false);
            foreach (string s in toLog ?? new string[0])
                Log(s, false);
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
