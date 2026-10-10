using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TaskManagerApp
{
    public enum Priority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Critical = 4
    }

    public enum Status
    {
        New = 0,
        InProgress = 1,
        Done = 2,
        Cancelled = 3
    }

    public class TaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public Priority Priority { get; set; }
        public Status Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string> Tags { get; set; }

        public TaskItem()
        {
            Tags = new List<string>();
            CreatedAt = DateTime.Now;
            Priority = Priority.Normal;
            Status = Status.New;
        }

        public override string ToString()
        {
            return string.Format("[{0}] {1} (P:{2}, S:{3})",
                Id, Title, Priority, Status);
        }

        public string ToCsv()
        {
            return string.Join("|",
                Id.ToString(),
                Title ?? "",
                Description ?? "",
                Priority.ToString(),
                Status.ToString(),
                CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                DueDate.HasValue ? DueDate.Value.ToString("yyyy-MM-dd") : "",
                string.Join(",", Tags));
        }

        public static TaskItem FromCsv(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            var parts = line.Split('|');
            if (parts.Length < 8) return null;

            var t = new TaskItem();
            t.Id = int.Parse(parts[0]);
            t.Title = parts[1];
            t.Description = parts[2];
            t.Priority = (Priority)Enum.Parse(typeof(Priority), parts[3]);
            t.Status = (Status)Enum.Parse(typeof(Status), parts[4]);
            t.CreatedAt = DateTime.Parse(parts[5]);
            t.DueDate = string.IsNullOrEmpty(parts[6]) ? (DateTime?)null : DateTime.Parse(parts[6]);
            if (!string.IsNullOrEmpty(parts[7]))
                t.Tags = parts[7].Split(',').ToList();
            return t;
        }
    }

    public class TaskRepository
    {
        private readonly List<TaskItem> _tasks = new List<TaskItem>();
        private int _nextId = 1;
        private readonly string _filePath;

        public TaskRepository(string filePath)
        {
            _filePath = filePath;
            if (File.Exists(filePath))
                Load();
        }

        public int Count
        {
            get { return _tasks.Count; }
        }

        public TaskItem Add(string title, string description, Priority priority)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title cannot be empty");

            var t = new TaskItem();
            t.Id = _nextId++;
            t.Title = title;
            t.Description = description;
            t.Priority = priority;
            _tasks.Add(t);
            return t;
        }

        public TaskItem FindById(int id)
        {
            for (int i = 0; i < _tasks.Count; i++)
            {
                if (_tasks[i].Id == id)
                    return _tasks[i];
            }
            return null;
        }

        public List<TaskItem> GetAll()
        {
            return new List<TaskItem>(_tasks);
        }

        public List<TaskItem> GetByStatus(Status status)
        {
            var result = new List<TaskItem>();
            foreach (var t in _tasks)
            {
                if (t.Status == status)
                    result.Add(t);
            }
            return result;
        }

        public List<TaskItem> GetByPriority(Priority priority)
        {
            return _tasks.Where(t => t.Priority == priority).ToList();
        }

        public List<TaskItem> Search(string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
                return new List<TaskItem>();

            keyword = keyword.ToLower();
            var result = new List<TaskItem>();
            foreach (var t in _tasks)
            {
                bool inTitle = t.Title != null && t.Title.ToLower().Contains(keyword);
                bool inDesc = t.Description != null && t.Description.ToLower().Contains(keyword);
                bool inTags = t.Tags.Any(tag => tag.ToLower().Contains(keyword));
                if (inTitle || inDesc || inTags)
                    result.Add(t);
            }
            return result;
        }

        public bool Remove(int id)
        {
            var t = FindById(id);
            if (t == null)
                return false;
            _tasks.Remove(t);
            return true;
        }

        public bool SetStatus(int id, Status status)
        {
            var t = FindById(id);
            if (t == null)
                return false;
            t.Status = status;
            return true;
        }

        public bool SetPriority(int id, Priority priority)
        {
            var t = FindById(id);
            if (t == null)
                return false;
            t.Priority = priority;
            return true;
        }

        public void AddTag(int id, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return;
            var t = FindById(id);
            if (t == null)
                return;
            if (!t.Tags.Contains(tag))
                t.Tags.Add(tag);
        }

        public void Save()
        {
            var sb = new StringBuilder();
            foreach (var t in _tasks)
            {
                sb.AppendLine(t.ToCsv());
            }
            File.WriteAllText(_filePath, sb.ToString(), Encoding.UTF8);
        }

        public void Load()
        {
            _tasks.Clear();
            _nextId = 1;
            var lines = File.ReadAllLines(_filePath, Encoding.UTF8);
            foreach (var line in lines)
            {
                var t = TaskItem.FromCsv(line);
                if (t != null)
                {
                    _tasks.Add(t);
                    if (t.Id >= _nextId)
                        _nextId = t.Id + 1;
                }
            }
        }

        public Dictionary<Status, int> GetStatistics()
        {
            var stats = new Dictionary<Status, int>();
            foreach (Status s in Enum.GetValues(typeof(Status)))
                stats[s] = 0;

            foreach (var t in _tasks)
                stats[t.Status]++;

            return stats;
        }

        public List<TaskItem> GetOverdue()
        {
            var now = DateTime.Now;
            return _tasks.Where(t =>
                t.DueDate.HasValue &&
                t.DueDate.Value < now &&
                t.Status != Status.Done &&
                t.Status != Status.Cancelled).ToList();
        }

        public List<TaskItem> SortByPriority()
        {
            return _tasks.OrderByDescending(t => t.Priority).ThenBy(t => t.Id).ToList();
        }
    }

    public class ConsoleUI
    {
        private readonly TaskRepository _repo;

        public ConsoleUI(TaskRepository repo)
        {
            _repo = repo;
        }

        public void Run()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  Task Manager v1.0");
            Console.WriteLine("========================================");

            bool running = true;
            while (running)
            {
                PrintMenu();
                string choice = Console.ReadLine();
                running = HandleChoice(choice);
            }

            _repo.Save();
            Console.WriteLine("Data saved. Bye!");
        }

        private void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("--- Menu ---");
            Console.WriteLine("1. Add task");
            Console.WriteLine("2. List all tasks");
            Console.WriteLine("3. Search");
            Console.WriteLine("4. Filter by status");
            Console.WriteLine("5. Filter by priority");
            Console.WriteLine("6. Change status");
            Console.WriteLine("7. Remove task");
            Console.WriteLine("8. Statistics");
            Console.WriteLine("9. Sort by priority");
            Console.WriteLine("0. Exit");
            Console.Write("Choose: ");
        }

        private bool HandleChoice(string choice)
        {
            switch (choice)
            {
                case "1": AddTask(); break;
                case "2": ListAll(); break;
                case "3": Search(); break;
                case "4": FilterStatus(); break;
                case "5": FilterPriority(); break;
                case "6": ChangeStatus(); break;
                case "7": RemoveTask(); break;
                case "8": PrintStatistics(); break;
                case "9": PrintSorted(); break;
                case "0": return false;
                default: Console.WriteLine("Unknown option"); break;
            }
            return true;
        }

        private void AddTask()
        {
            Console.Write("Title: ");
            string title = Console.ReadLine();
            Console.Write("Description: ");
            string desc = Console.ReadLine();
            Console.Write("Priority (1=Low, 2=Normal, 3=High, 4=Critical): ");
            int p;
            if (!int.TryParse(Console.ReadLine(), out p) || p < 1 || p > 4)
                p = 2;

            var t = _repo.Add(title, desc, (Priority)p);
            Console.WriteLine("Task added: " + t.ToString());
        }

        private void ListAll()
        {
            var all = _repo.GetAll();
            if (all.Count == 0)
            {
                Console.WriteLine("No tasks.");
                return;
            }
            foreach (var t in all)
                Console.WriteLine(t.ToString());
        }

        private void Search()
        {
            Console.Write("Keyword: ");
            string kw = Console.ReadLine();
            var found = _repo.Search(kw);
            Console.WriteLine("Found: " + found.Count);
            foreach (var t in found)
                Console.WriteLine(t.ToString());
        }

        private void FilterStatus()
        {
            Console.Write("Status (0=New, 1=InProgress, 2=Done, 3=Cancelled): ");
            int s;
            if (!int.TryParse(Console.ReadLine(), out s))
                return;
            var list = _repo.GetByStatus((Status)s);
            foreach (var t in list)
                Console.WriteLine(t.ToString());
        }

        private void FilterPriority()
        {
            Console.Write("Priority (1-4): ");
            int p;
            if (!int.TryParse(Console.ReadLine(), out p))
                return;
            var list = _repo.GetByPriority((Priority)p);
            foreach (var t in list)
                Console.WriteLine(t.ToString());
        }

        private void ChangeStatus()
        {
            Console.Write("Task Id: ");
            int id;
            if (!int.TryParse(Console.ReadLine(), out id))
                return;
            Console.Write("New status (0-3): ");
            int s;
            if (!int.TryParse(Console.ReadLine(), out s))
                return;
            if (_repo.SetStatus(id, (Status)s))
                Console.WriteLine("Status updated");
            else
                Console.WriteLine("Task not found");
        }

        private void RemoveTask()
        {
            Console.Write("Task Id: ");
            int id;
            if (!int.TryParse(Console.ReadLine(), out id))
                return;
            if (_repo.Remove(id))
                Console.WriteLine("Task removed");
            else
                Console.WriteLine("Task not found");
        }

        private void PrintStatistics()
        {
            var stats = _repo.GetStatistics();
            Console.WriteLine("--- Statistics ---");
            foreach (var kv in stats)
                Console.WriteLine("{0}: {1}", kv.Key, kv.Value);
            Console.WriteLine("Total: " + _repo.Count);

            var overdue = _repo.GetOverdue();
            if (overdue.Count > 0)
            {
                Console.WriteLine("--- Overdue ---");
                foreach (var t in overdue)
                    Console.WriteLine(t.ToString());
            }
        }

        private void PrintSorted()
        {
            var sorted = _repo.SortByPriority();
            foreach (var t in sorted)
                Console.WriteLine(t.ToString());
        }
    }

    public static class Program
    {
        public static void Main(string[] args)
        {
            string dataFile = "tasks.dat";

            if (args.Length > 0)
                dataFile = args[0];

            var repo = new TaskRepository(dataFile);
            var ui = new ConsoleUI(repo);
            ui.Run();
        }
    }
}
