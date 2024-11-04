using System.Drawing;
using System.Drawing.Imaging;

namespace ZombieSim
{
    internal class Program
    {
        static void Main(string[] args)
        {
            SimulationParameters parameters;
            parameters.size = 64;
            parameters.numAgents = 16000;
            parameters.startingZombies = 100;
            parameters.meetingCap = 100;
            string folder = @"D:\Pictures\generated\zombies";

            using StreamWriter stream = File.CreateText(Path.Combine(folder, "results.txt"));
            for (parameters.zombieSpeed = 30; parameters.zombieSpeed <= 100; parameters.zombieSpeed += 5)
                for (parameters.honor = 0; parameters.honor <= 80; parameters.honor += 5)
                    for (parameters.humanAgrressiveness = 25; parameters.humanAgrressiveness <= 65; parameters.humanAgrressiveness += 5)
                        for (parameters.retaliationChance = 0; parameters.retaliationChance <= 50; parameters.retaliationChance += 5)
                        {
                            stream.Write($"{parameters.zombieSpeed} | {parameters.honor} | {parameters.humanAgrressiveness} | {parameters.retaliationChance}");
                            for (int i = 0; i < 10; i++)
                            {
                                Simulation sim = new(parameters);
                                int t = sim.Run(10000, folder, i);
                                stream.Write($" | {t}");
                            }
                            stream.WriteLine();
                        }
        }
        struct SimulationParameters
        {
            public int size;
            public int numAgents;
            public int startingZombies;
            public int zombieSpeed;
            public int meetingCap;
            public int honor;
            public int humanAgrressiveness;
            public int retaliationChance;
        }
        class Simulation
        {
            static SimulationParameters Parameters;
            private readonly List<int>[] cells;
            int activeAgents;
            readonly Agent[] agents;
            int numHumans;
            readonly int zombieAddon;
            readonly int zombieRandMax;
            readonly Random rand;

            public Simulation(SimulationParameters parameters)
            {
                Parameters = parameters;
                cells = new List<int>[parameters.size * parameters.size];
                activeAgents = parameters.numAgents;
                agents = new Agent[parameters.numAgents];
                numHumans = parameters.numAgents - parameters.startingZombies;
                zombieAddon = (68 * parameters.zombieSpeed) / (100 - parameters.zombieSpeed);
                rand = new();
                zombieRandMax = zombieAddon + 68;

                for (int i = 0; i < parameters.size * parameters.size; i++) cells[i] = new List<int>();
                (ushort, ushort) randomPosition() => ((ushort)rand.Next(Parameters.size), (ushort)rand.Next(Parameters.size));
                uint middleidx = (uint)(((parameters.size + 1) * parameters.size) / 2);
                for (int i = 0; i < numHumans; i++)
                {
                    agents[i].position = randomPosition();
                    cells[agents[i].uposition].Add(i);
                }
                for (int i = numHumans; i < Parameters.numAgents; i++)
                {
                    agents[i].uposition = middleidx;
                    cells[middleidx].Add(i);
                }
#pragma warning disable CA1416 // Validate platform compatibility
                bitmap = new(parameters.size, parameters.size);
#pragma warning restore CA1416 // Validate platform compatibility
            }
            public int Run(int maxTurns, string folder, int number)
            {
                folder = Path.Combine(folder, $"s{Parameters.zombieSpeed}h{Parameters.honor}a{Parameters.humanAgrressiveness}r{Parameters.retaliationChance}_{number}");
                if (!Path.Exists(folder)) Directory.CreateDirectory(folder);
                for (int t = 0; t < maxTurns; t++)
                {
                    Step();
                    ExportImage(Path.Combine(folder,t.ToString("D4")));
                    if (numHumans == 0) return t;
                    if (numHumans == activeAgents) return -t;
                }
                return 0;
            }
            public void Step()
            {
                RandomWalk();
                RangedAttacks();
                MeleeCombat();
            }
            private void RandomWalk()
            {
                ushort bsize = (ushort)Parameters.size;
                ushort max_size = (ushort)(Parameters.size-1);
                (int, int) delta(int roll)
                {
                    if (roll < 7) return (-1, -1);
                    if (roll < 17) return (0, -1);
                    if (roll < 24) return (1, -1);
                    if (roll < 34) return (-1, 0);
                    if (roll < 44) return (1, 0);
                    if (roll < 51) return (-1, 1);
                    if (roll < 61) return (0, 1);
                    return (1, 1);
                }
                int dx, dy;
                ushort clamp(int val)
                {
                    if (val < 0) return 0;
                    if (val == bsize) return max_size;
                    return (ushort)val;
                }
                for (int i = 0; i < numHumans; i++)
                {
                    int rnd = rand.Next(78);
                    if (rnd < 10) continue;
                    rnd -= 10;
                    cells[agents[i].uposition].Remove(i);
                    ushort x, y;
                    (x, y) = agents[i].position;
                    (dx, dy) = delta(rnd);
                    agents[i].position = (clamp(x + dx), clamp(y + dy));
                    cells[agents[i].uposition].Add(i);
                }
                for (int i = numHumans; i < activeAgents; i++)
                {
                    int rnd = rand.Next(zombieRandMax);
                    if (rnd < zombieAddon) continue;
                    rnd -= zombieAddon;
                    cells[agents[i].uposition].Remove(i);
                    ushort x, y;
                    (x, y) = agents[i].position;
                    (dx, dy) = delta(rnd);  
                    agents[i].position = (clamp(x + dx), clamp(y + dy));
                    cells[agents[i].uposition].Add(i);
                }
            }
            private void RangedAttacks()
            {
                for (int i = 0; i < numHumans; i++)
                {
                    int numZombies = cells[agents[i].uposition].Count(id => id >= numHumans);
                    // does the human encounter a zombie
                    if (rand.Next(Parameters.meetingCap) >= numZombies) continue;
                    // does the human attack
                    if (rand.Next(100) >= Parameters.humanAgrressiveness) continue;
                    if (rand.Next(100+Parameters.zombieSpeed) < 100)
                    {
                        int killedZombie =
                            cells[agents[i].uposition].First(id => id >= numHumans);
                        activeAgents--;
                        cells[agents[i].uposition].Remove(killedZombie);
                        cells[agents[activeAgents].uposition].Remove(activeAgents);
                        cells[agents[activeAgents].uposition].Add(killedZombie);
                        (agents[activeAgents], agents[killedZombie]) = (agents[killedZombie], agents[activeAgents]);
                    }
                }
            }
            private void MeleeCombat()
            {
                for (int i = numHumans; i < activeAgents; i++)
                {
                    int humans = cells[agents[i].uposition].Count(id => id < numHumans);
                    // does the zombie encounter a human
                    if (rand.Next(Parameters.meetingCap) >= humans) continue;
                    // does the zombie infect the human
                    if (rand.Next(100+Parameters.zombieSpeed) < Parameters.zombieSpeed)
                    {
                        int killedHuman = 
                            cells[agents[i].uposition].First(id => id < numHumans);
                        numHumans--;
                        cells[agents[i].uposition].Remove(killedHuman);
                        cells[agents[i].uposition].Add(numHumans);
                        cells[agents[numHumans].uposition].Remove(numHumans);
                        cells[agents[numHumans].uposition].Add(killedHuman);
                        (agents[numHumans], agents[killedHuman]) = (agents[killedHuman], agents[numHumans]);
                    // does the human commmit suicide or turn
                        if (rand.Next(100) < Parameters.honor)
                        {
                            activeAgents--;
                            cells[agents[i].uposition].Remove(numHumans);
                            cells[agents[activeAgents].uposition].Remove(activeAgents);
                            cells[agents[activeAgents].uposition].Add(numHumans);
                            (agents[activeAgents], agents[numHumans]) = (agents[numHumans], agents[activeAgents]);
                        }
                    }
                    // retaliation
                    if (rand.Next(100 + Parameters.zombieSpeed) < Parameters.retaliationChance)
                    {
                        activeAgents--;
                        cells[agents[i].uposition].Remove(i);
                        cells[agents[activeAgents].uposition].Remove(activeAgents);
                        cells[agents[activeAgents].uposition].Add(i);
                        (agents[activeAgents], agents[i]) = (agents[i], agents[activeAgents]);
                        i--;
                    }
                }
            }
            struct Agent
            {
                ushort x;
                ushort y;
#pragma warning disable IDE1006 // Naming Styles
                public uint uposition
                {
                    get => (uint)(x * Parameters.size + y);
                    set
                    {
                        x = (ushort)(value / Parameters.size);
                        y = (ushort)(value % Parameters.size);
                    }
                }
                public (ushort, ushort) position
                {
                    readonly get => (x,y);
                    set => (x,y) = value;
                }
#pragma warning restore IDE1006 // Naming Styles
            }
#pragma warning disable CA1416 // Validate platform compatibility
            readonly Bitmap bitmap;
            void ExportImage(string path)
            {
                static int clamp(int value) => value > 255 ? 255 : value;
                int size = Parameters.size;
                

                int humans, zombies;
                for (int x = 0; x < size; x++)
                {
                    for (int y = 0; y < size; y++)
                    {
                        int i = x * size + y;
                        humans = 0; zombies = 0;
                        foreach (int k in cells[i])
                        {
                            if (k < numHumans) humans++;
                            else if (k < activeAgents) zombies++;
                        }
                        Color col = Color.FromArgb(clamp(humans * 4), clamp(zombies * 4), 0);
                        bitmap.SetPixel(x,y, col);
                    }
                }
                bitmap.Save(path + ".png", ImageFormat.Png);
            }
#pragma warning restore CA1416 // Validate platform compatibility
        }
    }
}
