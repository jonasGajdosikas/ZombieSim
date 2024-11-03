using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace ZombieSim
{
    internal class Program
    {
        static void Main(string[] args)
        {

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
        }
        class Simulation(SimulationParameters parameters)
        {
            readonly SimulationParameters Parameters = parameters;
            private readonly List<int>[] cells = new List<int>[parameters.size * parameters.size];
            int activeAgents = parameters.numAgents;
            readonly Agent[] agents = new Agent[parameters.numAgents];
            int numHumans = parameters.numAgents - parameters.startingZombies;
            readonly int zombieAddon = (68 * parameters.zombieSpeed) / (100 - parameters.zombieSpeed);
            int zombieRandMax;
            readonly Random rand = new();

            public void Setup()
            {
                zombieRandMax = zombieAddon + 68;
                (ushort, ushort) randomPosition() => ((ushort)rand.Next(Parameters.size), (ushort)rand.Next(Parameters.size));
                ushort middle = (ushort)(Parameters.size / 2);
                uint middleidx = (uint)middle << 16 | middle;
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
            }
            public void Run(int maxTurns)
            {
                Setup();
                for (int t = 0; t < maxTurns; t++)
                {
                    Step();
                }
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
                    if (val == 0xff) return 0;
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
                    rnd -= 2;
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
                    if (rand.Next(Parameters.meetingCap) < numZombies) continue;
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
                    if (rand.Next(Parameters.meetingCap) < humans) continue;
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
                        if (rand.Next(100) >= Parameters.honor)
                        {
                            activeAgents--;
                            cells[agents[i].uposition].Remove(numHumans);
                            cells[agents[activeAgents].uposition].Remove(activeAgents);
                            cells[agents[activeAgents].uposition].Add(numHumans);
                            (agents[activeAgents], agents[numHumans]) = (agents[numHumans], agents[activeAgents]);
                        }
                    }
                    // retaliation
                    if (rand.Next(100 + Parameters.zombieSpeed) < 50)
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
                public uint uposition;
                public (ushort, ushort) position
                {
                    readonly get => ((ushort)(uposition >> 16), (ushort)(uposition & 0xffff));
                    set => uposition = (uint)value.Item1 << 16 | value.Item2;
                }
            }
            
        }
    }
}
