using Microsoft.VisualBasic;
using System.Collections.Immutable;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Net.Security;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
using System.Text;

internal class Program
{
    public static string NodeFile = "nodes.nds";
    public static string StateFile = "state.ste";

    private static void Main(string[] args)
    {
        Console.WriteLine(@"   _____ _                      _____      _            _       _             ");
        Console.WriteLine(@"  / ____| |                    / ____|    | |          | |     | |            ");
        Console.WriteLine(@" | |    | |__   ___  ___ ___  | |     __ _| | ___ _   _| | __ _| |_ ___  _ __ ");
        Console.WriteLine(@" | |    | '_ \ / _ \/ __/ __| | |    / _` | |/ __| | | | |/ _` | __/ _ \| '__|");
        Console.WriteLine(@" | |____| | | |  __/\__ \__ \ | |___| (_| | | (__| |_| | | (_| | || (_) | |   ");
        Console.WriteLine(@"  \_____|_| |_|\___||___/___/  \_____\__,_|_|\___|\__,_|_|\__,_|\__\___/|_|   ");
        Console.WriteLine(@"                                                                              ");
        Console.WriteLine(@"(c) Joshua Taylor 2023");
        Console.WriteLine();

        bool mainLoop = true;

        while (mainLoop)
        {
            int option = RunMenu();

            // Quit
            if (option == 4)
            {
                mainLoop = false;
                Console.WriteLine("Bye :)");
            }

            // Play
            if (option == 1)
            {
                Board board = Board.DefaultBoard;

                bool quit = false;

                while (!quit)
                {
                    List<Move> moves = board.CalculateValidMoves().ToList();
                    Console.WriteLine(board);

                    if (moves.Count == 0)
                    {
                        Console.WriteLine($"Checkmate. {(board.whiteTurn ? "Black" : "White")} wins.");
                        quit = true;
                        break;
                    }

                    Console.WriteLine($"There are {moves.Count} moves for {(board.whiteTurn ? "white" : "black")} this turn");
                    (byte row, byte col) = GetSpace("Pick your piece: ");

                    Piece p = board.GetPiece(row, col);

                    if (p == null)
                    {
                        Console.WriteLine("No piece found there");
                        continue;
                    }
                    else
                    {
                        if (p.white != board.whiteTurn)
                        {
                            Console.WriteLine("Wrong team");
                            continue;
                        }
                        List<Move> pieceMoves = moves.Where(m => m.lastRow == p.Row && m.lastCol == p.Col).ToList();
                        if (pieceMoves.Count == 0)
                        {
                            Console.WriteLine("No moves for this piece. Pick another.");
                            continue;
                        }
                        (row, col) = GetSpace($"Move {p.type} to ({string.Join(", ", pieceMoves.Select(m => ((char)(m.col + 'a')).ToString() + ((char)(m.row + '1'))))}): ");

                        bool moved = false;
                        foreach (Move m in pieceMoves)
                        {
                            if (m.row == row && m.col == col)
                            {
                                board.MovePiece(m);
                                moved = true;
                                break;
                            }
                        }
                        if (!moved)
                            Console.WriteLine("Invalid move");
                    }
                }
            }

            // Run sims
            if (option == 2)
            {
                Console.WriteLine();
                Console.WriteLine("How many levels do you want to simulate?");

                string? input;
                int result;
                do
                {
                    Console.Write(">");
                    input = Console.ReadLine();
                } while (int.TryParse(input, out result) && (result < 1 || result > 10));

                // Do the sims
                RunSimulations(result);

                Console.WriteLine();
            }

            // Progressive sims
            if (option == 3)
            {
                Console.WriteLine();
                Console.WriteLine("How many hundred thousands of boards do you want to simulate?");

                string? input;
                int result;
                do
                {
                    Console.Write(">");
                    input = Console.ReadLine();
                } while (int.TryParse(input, out result) && (result < 1));

                // Do the sims
                RunProgressiveSimulations(result);
            }
        }
    }

    private static int RunMenu()
    {
        Console.WriteLine();
        Console.WriteLine("Whatcha wanna do?");
        Console.WriteLine("  1. Play da chess");
        Console.WriteLine("  2. Do some sims");
        Console.WriteLine("  3. PROGRESSIVE sims");
        Console.WriteLine("  4. Die");
        Console.WriteLine();

        string? input;
        int result;
        do
        {
            Console.Write(">");
            input = Console.ReadLine();
        } while (int.TryParse(input, out result) && (result < 1 || result > 4));
        return result;
    }

    public class ByteArrayComparer : IComparer<byte[]>
    {
        public int Compare(byte[]? a, byte[]? b)
        {
            int result;
            for (int index = 0; index < Math.Min(a.Length, b.Length); index++)
            {
                result = a[index].CompareTo(b[index]);
                if (result != 0) return result;
            }
            return a.Length.CompareTo(b.Length);
        }
    }

    public class NodeComparer : IComparer<Node>
    {
        public int Compare(Node? a, Node? b)
        {
            if (a != null && b != null)
            {
                int result;
                for (int index = 0; index < Math.Min(a.board.Length, b.board.Length); index++)
                {
                    result = a.board[index].CompareTo(b.board[index]);
                    if (result != 0) return result;
                }
                return a.board.Length.CompareTo(b.board.Length);
            }

            if (a == null)
            {
                if (b == null) return 0;
                else return 1;
            }
            else return -1;
        }
    }

    public class NodeEqualityComparer : IEqualityComparer<Node>
    {
        public bool Equals(Node? a, Node? b)
        {
            if (a != null && b != null)
            {
                return Board.Compare(a.board, b.board);
            }

            if (a == null)
            {
                if (b == null) return true;
            }
            return false;
        }

        int IEqualityComparer<Node>.GetHashCode(Node obj)
        {
            return (int)obj.address;
        }
    }

    private static void RunSimulations(int levels)
    {
        Board board = Board.DefaultBoard;

        /*
        byte[] compressed = board.Compress();
        board.Decompress(compressed);
        byte[] compressed2 = board.Compress();

        Console.WriteLine(Convert.ToHexString(compressed) + $" ({compressed.Length})");
        Console.WriteLine(Convert.ToHexString(compressed2) + $" ({compressed2.Length})");
        Console.WriteLine(Board.Compare(compressed, compressed2));
        Console.WriteLine(board);
        return;
        */

        List<Node> nodes = new List<Node>();
        nodes.Add(new Node());
        nodes[0].address = 0;
        nodes[0].level = 0;
        nodes[0].board = board.Compress();

        Console.WriteLine();
        Board nodeBoard;
        byte[] compressedBoard;
        int index = 0;
        int insertIndex = 0;
        int savedBoards = 0;
        int currentLevelBoards = 1;
        int totalBoards = 0;
        int printedDigits = 0;
        Node newNode = new Node();
        Node[] tempNodes = new Node[1];


        Stopwatch main = new();
        main.Start();

        Stopwatch sub1 = new();
        Stopwatch sub2 = new();
        Stopwatch sub3 = new();
        Stopwatch sub4 = new();
        Stopwatch sub5 = new();
        Stopwatch sub6 = new();

        Console.Write("1");
        // Loop for however many moves we chose to make
        for (int i = 0; i < levels; i++)
        {
            // Find the boards that we want

            // First grow the list
            tempNodes = new Node[nodes.Count];
            nodes.CopyTo(tempNodes);
            nodes = new List<Node>(nodes.Count + (currentLevelBoards * 30));
            nodes.AddRange(tempNodes);
            tempNodes = null;

            // Then reset our count
            currentLevelBoards = 0;
            Console.Write("->");

            // Run a simulation for every node in the list
            for (int x = 0; x < nodes.Count; x++)
            {
                if (nodes[x].level != i)
                    continue;

                //nodeIndex = levelNodes[x];
                // Don't run if it already has children or is at the end
                if (nodes[x].isEnd || nodes[x].children.Count > 0)
                    continue;

                // Grab the board from the list
                nodeBoard = new Board();
                nodeBoard.Decompress(nodes[x].board);

                sub2.Start();
                // Simulate the boards
                foreach (Board b in nodeBoard.CalculateValidBoards())
                {
                    sub2.Stop();
                    currentLevelBoards++;
                    totalBoards++;
                    compressedBoard = b.Compress();

                    newNode = new Node(compressedBoard, (ushort)(i + 1));
                    index = nodes.BinarySearch(newNode, new NodeComparer());

                    sub5.Start();
                    // Found
                    if (index >= 0)
                    {
                        nodes[x].children.Add(nodes[index].address);
                        savedBoards++;
                    }
                    // Not found, add
                    else
                    {
                        insertIndex = ~index;
                        newNode.address = (ulong)nodes.Count;
                        nodes.Insert(insertIndex, newNode);
                        if (x >= insertIndex)
                            x++;
                        nodes[x].children.Add(newNode.address);
                    }

                    sub5.Stop();

                    // Update the console periodically
                    if (currentLevelBoards % 10000 == 0)
                    {
                        string formatted = currentLevelBoards.ToString();
                        Console.Write(new string('\b', printedDigits) + formatted);
                        printedDigits = formatted.Length;
                    }
                    sub2.Start();
                }
                sub2.Stop();

                // If we don't have any child nodes, make sure to mark it as a dead end
                if (nodes[x].children.Count == 0)
                    nodes[x].isEnd = true;
            }

            Console.Write(new string('\b', printedDigits) + currentLevelBoards);
            printedDigits = 0;
        }

        main.Stop();

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"Calculated {nodes.Count} unique boards for {levels} moves " +
            $"({Math.Ceiling((double)levels / 2)} white, {Math.Floor((double)levels / 2)} black)");
        Console.WriteLine($"Saved {savedBoards} boards ({Math.Round((double)savedBoards / totalBoards * 100)}%) by checking if they're the same");
        Console.WriteLine("-----------------");
        Console.WriteLine($"Calculated {totalBoards} total boards in {main.Elapsed}");
        Console.WriteLine();
        Console.WriteLine($"sub2: {sub2.Elapsed}");
        Console.WriteLine($"sub5: {sub5.Elapsed}");
        Console.WriteLine($"other: {main.Elapsed - (sub1.Elapsed + sub2.Elapsed + sub3.Elapsed + sub4.Elapsed + sub5.Elapsed + sub6.Elapsed)}");
    }

    private static void RunProgressiveSimulations(int hundThous)
    {
        Board board = Board.DefaultBoard;

        Board nodeBoard;
        byte[] compressedBoard;
        int index = 0;
        int insertIndex = 0;
        int savedBoards = 0;
        int sameLevelSavedBoards = 0;
        int currentLevelBoards = 1;
        int totalBoards = 0;
        int printedDigits = 0;
        int x = 0;
        Node newNode = new Node();
        Node[] tempNodes = new Node[1];
        SortedSet<Node> levelNodes = new();

        bool finishedLevel = false;

        Stopwatch main = new();

        Stopwatch sub1 = new();
        Stopwatch sub2 = new();
        Stopwatch sub3 = new();
        Stopwatch sub4 = new();
        Stopwatch sub5 = new();
        Stopwatch sub6 = new();

        Console.WriteLine();

        sub1.Start();
        (int currentX, int currentLevel) = LoadProgressiveState();
        //SortedSet<Node> nodes = new SortedSet<Node>(new NodeComparer());
        LoadNodes(out SortedSet<Node> nodes);
        sub1.Stop();

        Console.WriteLine($"Loaded {nodes.Count} boards in {sub1.Elapsed}");
        Console.WriteLine($"currentX: {currentX}, currentLevel: {currentLevel}");
        sub1.Reset();

        if (nodes.Count == 0)
        {
            nodes.Add(
                new Node()
                {
                    address = 0,
                    level = 0,
                    board = board.Compress()
                }
            );
        }

        Console.WriteLine();

        main.Start();

        // Loop for however many moves we chose to make
        while (totalBoards < hundThous * 100000)
        {
            Console.Write(currentLevel + 1);

            // First grow the list
            /*
            tempNodes = new Node[nodes.Count];
            nodes.CopyTo(tempNodes);
            nodes = new List<Node>(nodes.Count + (currentLevelBoards * 30));
            nodes.AddRange(tempNodes);
            tempNodes = null;
            */

            // Then reset our count
            currentLevelBoards = 0;
            x = 0;
            finishedLevel = true;
            Console.Write("->");
            levelNodes.Clear();

            // Run a simulation for every node in the list
            //for (int x = currentX; x < nodes.Count; x++)
            foreach (Node node in nodes)
            {
                if (node.level != currentLevel)
                    continue;

                x++;

                if (x <= currentX)
                    continue;

                if (totalBoards >= hundThous * 100000)
                {
                    currentX = x;
                    finishedLevel = false;
                    break;
                }

                //nodeIndex = levelNodes[x];
                // Don't run if it already has children or is at the end
                if (node.isEnd || node.children.Count > 0)
                    continue;

                // Grab the board from the list
                sub1.Start();
                nodeBoard = new Board();
                nodeBoard.Decompress(node.board);
                sub1.Stop();

                sub2.Start();
                // Simulate the boards
                foreach (Board b in nodeBoard.CalculateValidBoards())
                {
                    sub2.Stop();
                    sub3.Start();
                    currentLevelBoards++;
                    totalBoards++;
                    compressedBoard = b.Compress();

                    sub3.Stop();
                    sub4.Start();

                    newNode = new Node(compressedBoard, (ushort)(currentLevel + 1));
                    bool found = nodes.TryGetValue(newNode, out Node match);
                    sub4.Stop();
                    sub5.Start();

                    // Found in the sorted list
                    if (found)
                    {
                        node.children.Add(match.address);
                        if (match.level == newNode.level)
                            sameLevelSavedBoards++;
                        else
                            savedBoards++;

                        newNode = null;
                    }
                    // Not found, check if its in the level nodes
                    else
                    {
                        if (levelNodes.TryGetValue(newNode, out match))
                        {
                            node.children.Add(match.address);
                            sameLevelSavedBoards++;

                            newNode = null;
                        }
                        else
                        {
                            newNode.address = (ulong)(nodes.Count + levelNodes.Count);
                            node.children.Add(newNode.address);
                            levelNodes.Add(newNode);
                        }
                    }

                    sub5.Stop();

                    // Update the console periodically
                    if (currentLevelBoards % 10000 == 0)
                    {
                        string formatted = currentLevelBoards.ToString();
                        Console.Write(new string('\b', printedDigits) + formatted);
                        printedDigits = formatted.Length;
                    }

                    sub2.Start();
                }
                sub2.Stop();

                // If we don't have any child nodes, make sure to mark it as a dead end
                if (node.children.Count == 0)
                    node.isEnd = true;
            }

            if (finishedLevel)
            {
                currentLevel++;
                currentX = 0;
            }

            // Add level nodes to our sorted set
            sub6.Start();
            nodes.UnionWith(levelNodes);

            levelNodes.Clear();
            GC.Collect();
            sub6.Stop();

            Console.WriteLine(new string('\b', printedDigits) + currentLevelBoards);
            printedDigits = 0;
        }

        main.Stop();

        Console.WriteLine();
        Console.WriteLine("Done.");

        ulong[] levelValues = new ulong[currentLevel + 2];
        foreach (Node n in nodes)
            levelValues[n.level]++;

        Console.WriteLine();

        foreach (ulong val in levelValues)
            Console.Write($"{val}->");

        Console.WriteLine("\b\b  ");

        Console.WriteLine();
        Console.WriteLine($"We have {nodes.Count} unique boards for up to {currentLevel + 1} moves " +
            $"({Math.Ceiling((double)(currentLevel + 1) / 2)} white, {Math.Floor((double)(currentLevel + 1) / 2)} black).");
        Console.WriteLine($"Calculated {totalBoards} boards for this round.");
        Console.WriteLine($"Saved {sameLevelSavedBoards + savedBoards} boards " +
            $"(~{Math.Round((double)(sameLevelSavedBoards + savedBoards) / totalBoards * 100)}%) by checking if they're the same.");
        Console.WriteLine($"~{Math.Round((double)sameLevelSavedBoards / (sameLevelSavedBoards + savedBoards) * 100)}% " +
            $"of the saved boards were amongst the same level.");
        Console.WriteLine("-----------------");
        Console.WriteLine($"Calculated these boards in {main.Elapsed}");
        Console.WriteLine();
        Console.WriteLine($"sub1: {sub1.Elapsed}");
        Console.WriteLine($"sub2: {sub2.Elapsed}");
        Console.WriteLine($"sub3: {sub3.Elapsed}");
        Console.WriteLine($"sub4: {sub4.Elapsed}");
        Console.WriteLine($"sub5: {sub5.Elapsed}");
        Console.WriteLine($"sub6: {sub6.Elapsed}");
        Console.WriteLine($"other: {main.Elapsed - (sub1.Elapsed + sub2.Elapsed + sub3.Elapsed + sub4.Elapsed + sub5.Elapsed + sub6.Elapsed)}");
        Console.WriteLine();

        sub1.Reset();
        sub1.Start();
        SaveNodes(nodes);
        SaveProgressiveState(currentX, currentLevel);
        sub1.Stop();

        Console.WriteLine($"Saved {nodes.Count} boards to disk in {sub1.Elapsed}");

        nodes.Clear();
        levelNodes.Clear();

        nodes = null;
        levelNodes = null;

        GC.Collect();
    }

    private static void LoadNodes(out SortedSet<Node> nodes)
    {
        //SortedSet<Node> nodes = new(new NodeComparer());
        nodes = new SortedSet<Node>(new NodeComparer());

        if (!File.Exists(NodeFile))
            return;// nodes;

        FileInfo fi = new FileInfo(NodeFile);
        long length = fi.Length;

        Console.Write("Reading... 00%");

        ulong num = 0;
        long startIndex = 0;

        using (StreamReader sr = new StreamReader(NodeFile))
        {
            char[] board = new char[39];
            char[] address = new char[8];
            char[] level = new char[2];
            byte childCountIsEnd = 0;

            char[] child = new char[8];

            while (!sr.EndOfStream)
            {
                Node n = new();

                sr.Read(board, 0, 39);
                sr.Read(address, 0, 8);
                sr.Read(level, 0, 2);
                childCountIsEnd = (byte)sr.Read();

                n.board = board.Select(c => (byte)c).ToArray();
                n.address = BitConverter.ToUInt64(address.Select(c => (byte)c).ToArray());
                n.level = BitConverter.ToUInt16(level.Select(c => (byte)c).ToArray());
                n.isEnd = childCountIsEnd >= 128;
                byte childCount = (byte)(childCountIsEnd - (n.isEnd ? 128 : 0));

                for (int i = 0; i < childCount; i++)
                {
                    sr.Read(child, 0, 8);
                    n.children.Add(BitConverter.ToUInt64(child.Select(c => (byte)c).ToArray()));
                }

                nodes.Add(n);

                if (num % 10000 == 0)
                {
                    Console.Write($"\b\b\b{Math.Floor((double)startIndex / length * 100.0):00}%");
                }
                num++;

                startIndex += 50 + childCount * 8;
            }
        }
        GC.Collect();

        Console.WriteLine("\b\b\b100%");

        //return nodes;
    }

    private static void SaveNodes(in SortedSet<Node> nodes)
    {
        ulong num = 0;
        Console.Write("Saving... 00%");

        using (StreamWriter sw = new StreamWriter(NodeFile))
        {
            foreach (Node node in nodes)
            {
                string line = new string(node.board.Select(c => (char)c).ToArray());
                line += new string(BitConverter.GetBytes(node.address).Select(c => (char)c).ToArray());
                line += new string(BitConverter.GetBytes(node.level).Select(c => (char)c).ToArray());
                line += (char)((node.isEnd ? 128 : 0) + node.children.Count);

                if (node.children.Count > 127)
                    throw new Exception("Too many children");

                foreach (ulong child in node.children)
                {
                    line += new string(BitConverter.GetBytes(child).Select(c => (char)c).ToArray());
                }

                sw.Write(line);

                if (num % 10000 == 0)
                {
                    Console.Write($"\b\b\b{Math.Floor((double)num / nodes.Count * 100.0):00}%");
                }
                num++;
            }
        }

        Console.WriteLine("\b\b\b100%");
        num = 0;
    }

    private static (int, int) LoadProgressiveState()
    {
        if (!File.Exists(StateFile))
            return (0, 0);

        byte[] data = File.ReadAllBytes(StateFile);

        return (BitConverter.ToInt32(data, 0), BitConverter.ToInt32(data, 4));
    }

    private static void SaveProgressiveState(int currentX, int currentLevel)
    {
        byte[] x = BitConverter.GetBytes(currentX);
        byte[] level = BitConverter.GetBytes(currentLevel);

        List<byte> returnList = x.ToList();
        returnList.AddRange(level);

        File.WriteAllBytes(StateFile, returnList.ToArray());
    }

    private static (byte, byte) GetSpace(string prompt)
    {
        bool valid = false;

        Console.Write(prompt);

        while (!valid)
        {
            string? s = Console.ReadLine();
            s = s?.ToLower();

            if (s?.Length == 2)
            {
                if (s[0] < 'a' || s[0] > 'h')
                {
                    Console.Write("Invalid column, please try again: ");
                    continue;
                }
                if (s[1] < '1' || s[1] > '8')
                {
                    Console.Write("Invalid row, please try again: ");
                    continue;
                }

                return ((byte)(s[1] - '1'), (byte)(s[0] - 'a'));
            }
            else
            {
                Console.Write("Invalid input, please try again: ");
            }
        }

        return (0, 0);
    }
}

class Node : IComparable<Node>
{
    public List<ulong> children = new();
    public byte[] board = new byte[39];
    public ulong address = 0;
    public ushort level = 0;
    public bool isEnd = false;

    public Node() { }

    public Node(byte[] board, ushort level) { this.board = board; this.level = level; }

    public Node(ulong address, ushort level) { this.address = address; this.level = level; }

    public int CompareTo(Node obj)
    {
        if (this != null && obj != null)
        {
            int result;
            for (int index = 0; index < Math.Min(this.board.Length, obj.board.Length); index++)
            {
                result = this.board[index].CompareTo(obj.board[index]);
                if (result != 0) return result;
            }
            return this.board.Length.CompareTo(obj.board.Length);
        }

        if (this == null)
        {
            if (obj == null) return 0;
            else return 1;
        }
        else return -1;
    }
}

class Piece
{
    public enum PieceType { Pawn = 'P', Rook = 'R', Knight = 'N', Bishop = 'B', Queen = 'Q', King = 'K' }

    public PieceType type;
    private byte row;
    private byte col;
    public bool white;
    public bool hasMoved = false;

    public byte Row { get => row; set { row = value; hasMoved = true; } }
    public byte Col { get => col; set { col = value; hasMoved = true; } }

    public Piece(PieceType type, byte row, byte col, bool white)
    {
        this.type = type;
        this.row = row;
        this.col = col;
        this.white = white;
    }

    public Piece(Piece p)
    {
        this.type = p.type;
        this.row = p.Row;
        this.col = p.Col;
        this.white = p.white;
        this.hasMoved = p.hasMoved;
    }

    public List<Move> CalculateMoves(in Board board)
    {
        byte curRow = row;
        byte curCol = col;

        int newRow;
        int newCol;

        List<Move> moves = new();

        // Pawn moves
        /// Up 1
        /// Diagonal 1 if take
        /// Up 2 if first move
        /// En Passant
        /// Pawn exchange
        if (type == PieceType.Pawn)
        {
            newRow = curRow + (white ? 1 : -1);
            newCol = curCol;
            if (!board.HasPiece((byte)newRow, (byte)newCol))
            {
                // Force a change if we're at the end
                if (curRow == (white ? 7 : 0))
                {
                    // Change to knight
                    moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol, PieceType.Knight));
                    // Change to bishop
                    moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol, PieceType.Bishop));
                    // Change to rook
                    moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol, PieceType.Rook));
                    // Change to queen
                    moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol, PieceType.Queen));
                }
                else
                {
                    // Straight move 1
                    moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }

                // If we're in the original position, straight move 2
                if (curRow == (white ? 1 : 6))
                {
                    newRow = curRow + (white ? 2 : -2);
                    if (!board.HasPiece((byte)newRow, (byte)newCol))
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
            }

            // Diagonal Take
            newRow = curRow + (white ? 1 : -1);
            newCol = curCol - 1;
            if (board.HasPiece((byte)newRow, (byte)newCol, !white))
                moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));

            newCol = curCol + 1;
            if (board.HasPiece((byte)newRow, (byte)newCol, !white))
                moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));

            // En Passant
            if (board.LastMove.row == curRow && board.LastMove.lastRow == (curRow + (white ? 2 : -2))
                && board.LastMove.col == newCol && board.GetPiece((byte)curRow, (byte)newCol).type == PieceType.Pawn)
                moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol, 'E'));

            newCol = curCol - 1;
            if (board.LastMove.row == curRow && board.LastMove.lastRow == (curRow + (white ? 2 : -2))
                && board.LastMove.col == newCol && board.GetPiece((byte)curRow, (byte)newCol).type == PieceType.Pawn)
                moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol, 'E'));


        }

        // Knight moves
        /// Up 1 over 2 x 4
        /// Up 2 over 1 x 4
        if (type == PieceType.Knight)
        {
            newRow = curRow - 2;
            newCol = curCol - 1;

            if (newRow >= 0)
            {
                if (newCol >= 0)
                {
                    if (!board.HasPiece((byte)newRow, (byte)newCol, white))
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
                newCol = curCol + 1;
                if (newCol <= 7)
                {
                    if (!board.HasPiece((byte)newRow, (byte)newCol, white))
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
            }

            newRow = curRow + 2;
            newCol = curCol + 1;
            if (newRow <= 7)
            {
                if (newCol <= 7)
                {
                    if (!board.HasPiece((byte)newRow, (byte)newCol, white))
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
                newCol = curCol - 1;
                if (newCol >= 0)
                {
                    if (!board.HasPiece((byte)newRow, (byte)newCol, white))
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
            }

            newRow = curRow - 1;
            newCol = curCol - 2;
            if (newRow >= 0)
            {
                if (newCol >= 0)
                {
                    if (!board.HasPiece((byte)newRow, (byte)newCol, white))
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
                newCol = curCol + 2;
                if (newCol <= 7)
                {
                    if (!board.HasPiece((byte)newRow, (byte)newCol, white))
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
            }

            newRow = curRow + 1;
            newCol = curCol + 2;
            if (newRow <= 7)
            {
                if (newCol <= 7)
                {
                    if (!board.HasPiece((byte)newRow, (byte)newCol, white))
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
                newCol = curCol - 2;
                if (newCol >= 0)
                {
                    if (!board.HasPiece((byte)newRow, (byte)newCol, white))
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
            }
        }

        // Bishop/Queen Moves
        /// Diagonal until there's another piece
        /// Includes piece if other team
        if (type == PieceType.Bishop || type == PieceType.Queen)
        {
            newRow = curRow;
            newCol = curCol;
            bool valid = true;

            // Down, Right
            while (valid)
            {
                newRow++;
                newCol++;

                if (newRow > 7 || newCol > 7)
                    break;

                if (!board.HasPiece((byte)newRow, (byte)newCol))
                {
                    moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
                else
                {
                    valid = false;
                    if (board.HasPiece((byte)newRow, (byte)newCol, !white))
                    {
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                    }
                }
            }

            newRow = curRow;
            newCol = curCol;
            valid = true;

            // Up, Left
            while (valid)
            {
                newRow++;
                newCol--;

                if (newRow > 7 || newCol < 0)
                    break;

                if (!board.HasPiece((byte)newRow, (byte)newCol))
                {
                    moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
                else
                {
                    valid = false;
                    if (board.HasPiece((byte)newRow, (byte)newCol, !white))
                    {
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                    }
                }
            }

            newRow = curRow;
            newCol = curCol;
            valid = true;

            // Down, Right
            while (valid)
            {
                newRow--;
                newCol++;

                if (newRow < 0 || newCol > 7)
                    break;

                if (!board.HasPiece((byte)newRow, (byte)newCol))
                {
                    moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
                else
                {
                    valid = false;
                    if (board.HasPiece((byte)newRow, (byte)newCol, !white))
                    {
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                    }
                }
            }

            newRow = curRow;
            newCol = curCol;
            valid = true;

            // Down, Left
            while (valid)
            {
                newRow--;
                newCol--;

                if (newRow < 0 || newCol < 0)
                    break;

                if (!board.HasPiece((byte)newRow, (byte)newCol))
                {
                    moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
                else
                {
                    valid = false;
                    if (board.HasPiece((byte)newRow, (byte)newCol, !white))
                    {
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                    }
                }
            }
        }


        // Rook/Queen Moves
        /// Linear until there's another piece
        /// Includes piece if other team
        if (type == PieceType.Rook || type == PieceType.Queen)
        {
            newRow = curRow;
            newCol = curCol;
            bool valid = true;

            // Up
            while (valid)
            {
                newRow++;

                if (newRow > 7)
                    break;

                if (!board.HasPiece((byte)newRow, (byte)newCol))
                {
                    moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
                else
                {
                    valid = false;
                    if (board.HasPiece((byte)newRow, (byte)newCol, !white))
                    {
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                    }
                }
            }

            newRow = curRow;
            valid = true;

            // Right
            while (valid)
            {
                newCol++;

                if (newCol > 7)
                    break;

                if (!board.HasPiece((byte)newRow, (byte)newCol))
                {
                    moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
                else
                {
                    valid = false;
                    if (board.HasPiece((byte)newRow, (byte)newCol, !white))
                    {
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                    }
                }
            }

            newCol = curCol;
            valid = true;

            // Down
            while (valid)
            {
                newRow--;

                if (newRow < 0)
                    break;

                if (!board.HasPiece((byte)newRow, (byte)newCol))
                {
                    moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
                else
                {
                    valid = false;
                    if (board.HasPiece((byte)newRow, (byte)newCol, !white))
                    {
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                    }
                }
            }

            newRow = curRow;
            valid = true;

            // Left
            while (valid)
            {
                newCol--;

                if (newCol < 0)
                    break;

                if (!board.HasPiece((byte)newRow, (byte)newCol))
                {
                    moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                }
                else
                {
                    valid = false;
                    if (board.HasPiece((byte)newRow, (byte)newCol, !white))
                    {
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
                    }
                }
            }
        }

        // King Moves
        /// All around by one space
        /// Castling
        if (type == PieceType.King)
        {
            newRow = curRow;
            newCol = curCol;

            newRow--;
            if (newRow >= 0 && !board.HasPiece((byte)newRow, (byte)newCol, white))
            {
                moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
            }
            newCol--;
            if (newRow >= 0 && newCol >= 0 && !board.HasPiece((byte)newRow, (byte)newCol, white))
            {
                moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
            }
            newRow++;
            if (newCol >= 0 && !board.HasPiece((byte)newRow, (byte)newCol, white))
            {
                moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
            }
            newRow++;
            if (newRow <= 7 && newCol >= 0 && !board.HasPiece((byte)newRow, (byte)newCol, white))
            {
                moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
            }
            newCol++;
            if (newRow <= 7 && !board.HasPiece((byte)newRow, (byte)newCol, white))
            {
                moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
            }
            newCol++;
            if (newRow <= 7 && newCol <= 7 && !board.HasPiece((byte)newRow, (byte)newCol, white))
            {
                moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
            }
            newRow--;
            if (newCol <= 7 && !board.HasPiece((byte)newRow, (byte)newCol, white))
            {
                moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
            }
            newRow--;
            if (newRow >= 0 && newCol <= 7 && !board.HasPiece((byte)newRow, (byte)newCol, white))
            {
                moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol));
            }

            // Castling
            if (!hasMoved)
            {
                // Queen side
                Piece leftRook = board.GetPiece(curRow, 0);

                if (leftRook != null && leftRook.type == PieceType.Rook && !leftRook.hasMoved)
                {
                    if (!board.HasPiece(curRow, 1) && !board.HasPiece(curRow, 2) && !board.HasPiece(curRow, 3))
                    {
                        newRow = curRow;
                        newCol = curCol - 2;
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol, 'C'));
                    }
                }

                // King side
                Piece rightRook = board.GetPiece(curRow, 7);

                if (rightRook != null && rightRook.type == PieceType.Rook && !rightRook.hasMoved)
                {
                    if (!board.HasPiece(curRow, 6) && !board.HasPiece(curRow, 5))
                    {
                        newRow = curRow;
                        newCol = curCol + 2;
                        moves.Add(new Move(curRow, curCol, (byte)newRow, (byte)newCol, 'C'));
                    }
                }
            }
        }

        return moves;
    }

    public bool ValidMove(Board board, byte row, byte col)
    {
        foreach (Move m in CalculateMoves(board))
        {
            if (m.row == row && m.col == col)
                return true;
        }
        return false;
    }

    public byte[] Compress()
    {
        byte first = (byte)(row << 3);
        first += (byte)col;
        return new byte[2] { first, (byte)type };
    }

    public void Decompress(byte[] input)
    {
        row = (byte)(input[0] >> 3);
        col = (byte)(input[0] - (byte)(row << 3));
        type = (Piece.PieceType)input[1];
    }

    public static byte TypeToByte(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn:
                return (byte)0;
            case PieceType.Queen:
                return (byte)1;
            case PieceType.King:
                return (byte)2;
            case PieceType.Rook:
                return (byte)3;
            case PieceType.Bishop:
                return (byte)4;
            case PieceType.Knight:
                return (byte)5;
            default:
                return (byte)6;
        }
    }

    public static PieceType ByteToType(byte type)
    {
        switch (type)
        {
            case 0:
                return PieceType.Pawn;
            case 1:
                return PieceType.Queen;
            case 2:
                return PieceType.King;
            case 3:
                return PieceType.Rook;
            case 4:
                return PieceType.Bishop;
            case 5:
                return PieceType.Knight;
            default:
                return PieceType.Pawn;
        }
    }

    public int CompareTo(Piece other)
    {
        // -1 prefers self
        // 1 prefers other
        if (this.row == other.row)
        {
            if (this.col == other.col)
                return 0;
            else if (this.col < other.col)
                return -1;
            else
                return 1;
        }
        else if (this.row < other.row)
            return -1;
        else
            return 1;
    }

    public override string ToString()
    {
        return (char)type + ((char)(col + 'a')).ToString() + ((char)(row + '1')).ToString();
    }
}

class Move
{
    public byte lastRow;
    public byte lastCol;
    public byte row;
    public byte col;
    public byte option;

    public Move(byte lastRow, byte lastCol, byte row, byte col)
    {
        this.lastRow = lastRow;
        this.lastCol = lastCol;
        this.row = row;
        this.col = col;
        this.option = 0;
    }

    // Pawn Exchange
    public Move(byte lastRow, byte lastCol, byte row, byte col, Piece.PieceType type)
    {
        this.lastRow = lastRow;
        this.lastCol = lastCol;
        this.row = row;
        this.col = col;
        this.option = (byte)type;
    }

    // Pawn Exchange
    public Move(byte lastRow, byte lastCol, byte row, byte col, char option)
    {
        this.lastRow = lastRow;
        this.lastCol = lastCol;
        this.row = row;
        this.col = col;
        this.option = (byte)option;
    }
}

class Board
{
    public static Board DefaultBoard = new(
        new List<Piece>()
        {
            new Piece(Piece.PieceType.Rook, 0, 0, true),
            new Piece(Piece.PieceType.Knight, 0, 1, true),
            new Piece(Piece.PieceType.Bishop, 0, 2, true),
            new Piece(Piece.PieceType.Queen, 0, 3, true),
            new Piece(Piece.PieceType.King, 0, 4, true),
            new Piece(Piece.PieceType.Bishop, 0, 5, true),
            new Piece(Piece.PieceType.Knight, 0, 6, true),
            new Piece(Piece.PieceType.Rook, 0, 7, true),
            new Piece(Piece.PieceType.Pawn, 1, 0, true),
            new Piece(Piece.PieceType.Pawn, 1, 1, true),
            new Piece(Piece.PieceType.Pawn, 1, 2, true),
            new Piece(Piece.PieceType.Pawn, 1, 3, true),
            new Piece(Piece.PieceType.Pawn, 1, 4, true),
            new Piece(Piece.PieceType.Pawn, 1, 5, true),
            new Piece(Piece.PieceType.Pawn, 1, 6, true),
            new Piece(Piece.PieceType.Pawn, 1, 7, true)
        },
        new List<Piece>()
        {
            new Piece(Piece.PieceType.Rook, 7, 0, false),
            new Piece(Piece.PieceType.Knight, 7, 1, false),
            new Piece(Piece.PieceType.Bishop, 7, 2, false),
            new Piece(Piece.PieceType.Queen, 7, 3, false),
            new Piece(Piece.PieceType.King, 7, 4, false),
            new Piece(Piece.PieceType.Bishop, 7, 5, false),
            new Piece(Piece.PieceType.Knight, 7, 6, false),
            new Piece(Piece.PieceType.Rook, 7, 7, false),
            new Piece(Piece.PieceType.Pawn, 6, 0, false),
            new Piece(Piece.PieceType.Pawn, 6, 1, false),
            new Piece(Piece.PieceType.Pawn, 6, 2, false),
            new Piece(Piece.PieceType.Pawn, 6, 3, false),
            new Piece(Piece.PieceType.Pawn, 6, 4, false),
            new Piece(Piece.PieceType.Pawn, 6, 5, false),
            new Piece(Piece.PieceType.Pawn, 6, 6, false),
            new Piece(Piece.PieceType.Pawn, 6, 7, false)
        });

    List<Piece> white = new();
    List<Piece> black = new();

    public bool whiteTurn = true;
    public Move LastMove = new Move(0, 0, 0, 0);

    public Board() { }

    public Board(List<Piece> white, List<Piece> black)
    {
        this.white = white;
        this.black = black;
    }

    public Board Clone()
    {
        return new()
        {
            white = white.Select(p => new Piece(p)).ToList(),
            black = black.Select(p => new Piece(p)).ToList(),
            LastMove = LastMove,
            whiteTurn = whiteTurn
        };
    }

    public void MovePiece(Move m)
    {
        bool moved = false;

        // Do the piece move
        if (whiteTurn)
        {
            foreach (Piece p in white)
            {
                if (p.Row == m.lastRow && p.Col == m.lastCol)
                {
                    // Do the move
                    p.Row = m.row;
                    p.Col = m.col;

                    moved = true;

                    if (m.option != 0)
                    {
                        switch ((char)m.option)
                        {
                            // Castle
                            case 'C':
                                // Also move rook
                                // Queen side
                                if (m.col < m.lastCol)
                                    MovePiece(new Move(m.row, 0, m.row, 3));
                                // King side
                                else
                                    MovePiece(new Move(m.row, 7, m.row, 5));
                                break;

                            // En Passant
                            case 'E':
                                // Remove piece that we're taking
                                foreach (Piece d in (whiteTurn ? black : white))
                                {
                                    if (d.Row == m.lastRow && d.Col == m.col)
                                    {
                                        (whiteTurn ? black : white).Remove(d);
                                        break;
                                    }
                                }
                                break;

                            //
                            // Pawn exchange
                            //
                            case (char)Piece.PieceType.Knight:
                            case (char)Piece.PieceType.Bishop:
                            case (char)Piece.PieceType.Rook:
                            case (char)Piece.PieceType.Queen:
                                p.type = (Piece.PieceType)m.option;
                                break;

                            default:
                                break;
                        }
                    }

                    break;
                }
            }
        }
        else
        {
            foreach (Piece p in black)
            {
                if (p.Row == m.lastRow && p.Col == m.lastCol)
                {
                    // Do the move
                    p.Row = m.row;
                    p.Col = m.col;

                    moved = true;

                    if (m.option != 0)
                    {
                        switch ((char)m.option)
                        {
                            // Castle
                            case 'C':
                                // Also move rook
                                // Queen side
                                if (m.col < m.lastCol)
                                    MovePiece(new Move(m.row, 0, m.row, 3));
                                // King side
                                else
                                    MovePiece(new Move(m.row, 7, m.row, 5));
                                break;

                            // En Passant
                            case 'E':
                                // Remove piece that we're taking
                                foreach (Piece d in (whiteTurn ? black : white))
                                {
                                    if (d.Row == m.lastRow && d.Col == m.col)
                                    {
                                        (whiteTurn ? black : white).Remove(d);
                                        break;
                                    }
                                }
                                break;

                            //
                            // Pawn exchange
                            //
                            case (char)Piece.PieceType.Knight:
                            case (char)Piece.PieceType.Bishop:
                            case (char)Piece.PieceType.Rook:
                            case (char)Piece.PieceType.Queen:
                                p.type = (Piece.PieceType)m.option;
                                break;

                            default:
                                break;
                        }
                    }

                    break;
                }
            }
        }

        if (moved)
        {
            // Check if we're taking a piece
            int existingPiece = GetPieceIndex(m.row, m.col, !whiteTurn);

            if (existingPiece != -1)
            {
                (whiteTurn ? black : white).RemoveAt(existingPiece);
            }

            // Record our move
            LastMove = m;
            whiteTurn = !whiteTurn;
        }
    }

    public bool ValidateMove(Move m)
    {
        if (m.lastRow < 0 || m.lastRow > 7 || m.lastCol < 0 || m.lastCol > 7)
            throw new Exception("Cannot move piece: Target piece out of range");

        if (m.row < 0 || m.row > 7 || m.col < 0 || m.col > 7)
            throw new Exception("Cannot move piece: Destination out of range");

        if (!HasPiece(m.lastRow, m.lastCol))
            throw new Exception("Cannot move piece: No piece found");

        Piece? p = (whiteTurn ? white : black).Where(p => p.Row == m.lastRow && p.Col == m.lastCol).FirstOrDefault();

        if (p == null)
            throw new Exception("Cannot move piece: Wrong team");

        if (HasPiece(m.row, m.col, whiteTurn))
            throw new Exception("Cannot move piece: Teammate in the way");

        return true;
    }

    public void AddPiece(Piece p)
    {
        if (!HasPiece(p.Row, p.Col))
        {
            if (p.white)
                white.Add(p);
            else
                black.Add(p);
        }
        else
        {
            throw new Exception("Unable to add piece to board: Piece already in that square");
        }
    }

    public bool HasPiece(byte row, byte col)
    {
        foreach (Piece p in black)
            if (p.Row == row && p.Col == col) return true;

        foreach (Piece p in white)
            if (p.Row == row && p.Col == col) return true;

        return false;
    }

    public bool HasPiece(byte row, byte col, bool whiteTeam)
    {
        if (whiteTeam)
            foreach (Piece p in white)
                if (p.Row == row && p.Col == col) return true; else { }
        else
            foreach (Piece p in black)
                if (p.Row == row && p.Col == col) return true;

        return false;
    }

    public int GetPieceIndex(byte row, byte col, bool whiteTeam)
    {
        if (whiteTeam)
            for (int i = 0; i < white.Count; i++)
                if (white[i].Row == row && white[i].Col == col) return i; else { }
        else
            for (int i = 0; i < black.Count; i++)
                if (black[i].Row == row && black[i].Col == col) return i;

        return -1;
    }

    public Piece GetPiece(byte row, byte col)
    {
        foreach (Piece p in black)
            if (p.Row == row && p.Col == col) return p;

        foreach (Piece p in white)
            if (p.Row == row && p.Col == col) return p;

        return null;
    }

    public IEnumerable<Move> CalculateValidMoves()
    {
        if (whiteTurn)
            for (int i = 0; i < white.Count; i++)
                foreach (Move m in white[i].CalculateMoves(this))
                    if (!CausesCheckToSelf(m))
                        yield return m;
                    else { }
        else
            for (int i = 0; i < black.Count; i++)
                foreach (Move m in black[i].CalculateMoves(this))
                    if (!CausesCheckToSelf(m))
                        yield return m;
                    else { }
    }

    public IEnumerable<Board> CalculateValidBoards()
    {
        Board? b;
        if (whiteTurn)
            for (int i = 0; i < white.Count; i++)
                foreach (Move m in white[i].CalculateMoves(this))
                {
                    b = MoveIfNotCheck(m);
                    if (b != null)
                        yield return b;
                }
        else
            for (int i = 0; i < black.Count; i++)
                foreach (Move m in black[i].CalculateMoves(this))
                {
                    b = MoveIfNotCheck(m);
                    if (b != null)
                        yield return b;
                }
    }

    public IEnumerable<Move> CalculateAllMoves(bool forWhite)
    {
        if (forWhite)
            foreach (Piece p in white)
                foreach (Move m in p.CalculateMoves(this))
                    yield return m;
        else
            foreach (Piece p in black)
                foreach (Move m in p.CalculateMoves(this))
                    yield return m;
    }

    public bool CausesCheckToSelf(Move m)
    {
        List<Piece> oldWhite = white.Select(p => new Piece(p)).ToList();
        List<Piece> oldBlack = black.Select(p => new Piece(p)).ToList();
        Move oldLastMove = LastMove;
        bool oldWhiteTurn = whiteTurn;

        MovePiece(m);
        whiteTurn = oldWhiteTurn;

        bool causesCheck = IsInCheck(whiteTurn);

        white = oldWhite.Select(p => new Piece(p)).ToList();
        black = oldBlack.Select(p => new Piece(p)).ToList();
        LastMove = oldLastMove;

        oldWhite.Clear();
        oldWhite = null;
        oldBlack.Clear();
        oldBlack = null;

        return causesCheck;
    }

    public Board? MoveIfNotCheck(Move m)
    {
        List<Piece> oldWhite = white.Select(p => new Piece(p)).ToList();
        List<Piece> oldBlack = black.Select(p => new Piece(p)).ToList();
        Move oldLastMove = LastMove;
        bool oldWhiteTurn = whiteTurn;

        MovePiece(m);

        bool causesCheck = IsInCheck(oldWhiteTurn);

        Board? returnBoard = null;

        if (!causesCheck)
        {
            returnBoard = this.Clone();
        }

        white = oldWhite;//.Select(p => new Piece(p)).ToList();
        black = oldBlack;//.Select(p => new Piece(p)).ToList();
        LastMove = oldLastMove;
        whiteTurn = oldWhiteTurn;

        /*
        oldWhite.Clear();
        oldWhite = null;
        oldBlack.Clear();
        oldBlack = null;
        */

        return returnBoard;
    }

    public bool IsInCheck(bool forWhite)
    {
        byte kingRow = 8, kingCol = 8;

        // Find king
        if (forWhite)
        {
            foreach (Piece p in white)
            {
                if (p.type == Piece.PieceType.King)
                {
                    kingRow = p.Row;
                    kingCol = p.Col;
                    break;
                }
            }
        }
        else
        {
            foreach (Piece p in black)
            {
                if (p.type == Piece.PieceType.King)
                {
                    kingRow = p.Row;
                    kingCol = p.Col;
                    break;
                }
            }
        }

        foreach (Move m in CalculateAllMoves(!forWhite))
            if (m.row == kingRow && m.col == kingCol)
                return true;

        return false;
    }

    public bool EnPassantable()
    {
        if (whiteTurn)
        {
            // Last move was black moving a pawn twice
            if (LastMove.row == 4 && LastMove.lastRow == 6 && LastMove.col == LastMove.lastRow)
            {
                int movedPiece = GetPieceIndex(LastMove.row, LastMove.col, false);
                if (movedPiece != -1 && black[movedPiece].type == Piece.PieceType.Pawn)
                {
                    int leftPiece = GetPieceIndex(LastMove.row, (byte)(LastMove.col - 1), true);
                    int rightPiece = GetPieceIndex(LastMove.row, (byte)(LastMove.col + 1), true);

                    if (leftPiece != -1 && white[leftPiece].type == Piece.PieceType.Pawn)
                    {
                        if (!CausesCheckToSelf(new Move(LastMove.row, (byte)(LastMove.col - 1), (byte)(LastMove.row + 1), LastMove.col, 'E')))
                            return true;
                    }
                    if (rightPiece != -1 && white[rightPiece].type == Piece.PieceType.Pawn)
                    {
                        if (!CausesCheckToSelf(new Move(LastMove.row, (byte)(LastMove.col + 1), (byte)(LastMove.row + 1), LastMove.col, 'E')))
                            return true;
                    }
                }
            }
        }
        else
        {
            // Last move was white moving a pawn twice
            if (LastMove.row == 3 && LastMove.lastRow == 1 && LastMove.col == LastMove.lastRow)
            {
                int movedPiece = GetPieceIndex(LastMove.row, LastMove.col, true);
                if (movedPiece != -1 && white[movedPiece].type == Piece.PieceType.Pawn)
                {
                    int leftPiece = GetPieceIndex(LastMove.row, (byte)(LastMove.col - 1), false);
                    int rightPiece = GetPieceIndex(LastMove.row, (byte)(LastMove.col + 1), false);

                    if (leftPiece != -1 && black[leftPiece].type == Piece.PieceType.Pawn)
                    {
                        if (!CausesCheckToSelf(new Move(LastMove.row, (byte)(LastMove.col - 1), (byte)(LastMove.row - 1), LastMove.col, 'E')))
                            return true;
                    }
                    if (rightPiece != -1 && black[rightPiece].type == Piece.PieceType.Pawn)
                    {
                        if (!CausesCheckToSelf(new Move(LastMove.row, (byte)(LastMove.col + 1), (byte)(LastMove.row - 1), LastMove.col, 'E')))
                            return true;
                    }
                }
            }
        }

        return false;
    }

    public (bool, bool, bool) Castleable(bool whiteTeam)
    {
        bool queenCastleMoved = true;
        bool kingMoved = true;
        bool kingCastleMoved = true;
        foreach (Piece p in (whiteTeam ? white : black))
        {
            if (p.type == Piece.PieceType.Rook)
            {
                if (!p.hasMoved)
                {
                    if (p.Col == 0)
                        queenCastleMoved = false;
                    else
                        kingCastleMoved = false;
                }
            }
            else if (p.type == Piece.PieceType.King)
            {
                if (!p.hasMoved)
                    kingMoved = false;
            }
        }
        return (queenCastleMoved, kingMoved, kingCastleMoved);
    }

    public byte[] Compress()
    {
        bool enPassantable = EnPassantable();
        byte header = (byte)((white.Count - 1 << 4) + black.Count - 1);
        byte header2 = (byte)((whiteTurn ? 1 << 7 : 0) + (enPassantable ? 1 << 6 : 0) + (enPassantable ? LastMove.col << 3 : 0));

        (bool wa, bool wb, bool wc) = Castleable(true);
        (bool ba, bool bb, bool bc) = Castleable(false);

        byte header3 = (byte)(((wa ? 1 : 0) << 7) + ((wb ? 1 : 0) << 6) + ((wc ? 1 : 0) << 5) + ((ba ? 1 : 0) << 4) + ((bb ? 1 : 0) << 3) + ((bc ? 1 : 0) << 2));

        byte[] whites = new byte[white.Count + (white.Count > 8 ? 2 : 1)];
        byte[] blacks = new byte[black.Count + (black.Count > 8 ? 2 : 1)];

        white.Sort((a, b) => a.CompareTo(b));
        black.Sort((a, b) => a.CompareTo(b));

        for (int i = 0; i < white.Count; i++)
        {
            byte type = Piece.TypeToByte(white[i].type);
            whites[i] = (byte)((white[i].Row << 5) + (white[i].Col << 2) + (type >> 1));
            whites[white.Count - 1 + (i > 7 ? 2 : 1)] += (byte)((type - ((type >> 1) << 1)) << (7 - (i % 8)));
        }

        for (int i = 0; i < black.Count; i++)
        {
            byte type = Piece.TypeToByte(black[i].type);
            blacks[i] = (byte)((black[i].Row << 5) + (black[i].Col << 2) + (type >> 1));
            blacks[black.Count - 1 + (i > 7 ? 2 : 1)] += (byte)((type - ((type >> 1) << 1)) << (7 - (i % 8)));
        }

        byte[] compressedBoard = new byte[39];

        short index = 0;
        compressedBoard[index++] = header;
        compressedBoard[index++] = header2;
        compressedBoard[index++] = header3;

        for (int i = 0; i < whites.Length; i++)
        {
            compressedBoard[index++] = whites[i];
        }

        for (int i = 0; i < blacks.Length; i++)
        {
            compressedBoard[index++] = blacks[i];
        }

        for (int i = index; i < 39; i++)
            compressedBoard[index++] = 0;

        whites = null;
        blacks = null;

        return compressedBoard;
    }

    public void Decompress(in byte[] compressed)
    {
        int whiteCount = (compressed[0] >> 4) + 1;
        int blackCount = compressed[0] - (whiteCount - 1 << 4) + 1;

        whiteTurn = (compressed[1] >> 7) == 1;
        bool enPassantable = (compressed[1] >> 6) % 2 == 1;

        if (enPassantable)
        {
            LastMove.col = (byte)((compressed[1] >> 3) - (whiteTurn ? 1 << 4 : 0) - (enPassantable ? 1 << 3 : 0));
            LastMove.row = (byte)(whiteTurn ? 4 : 3);
            LastMove.lastCol = LastMove.col;
            LastMove.lastRow = (byte)(whiteTurn ? 6 : 1);
        }

        white.Clear();
        black.Clear();

        byte type = 0;
        byte row = 0;
        byte col = 0;
        for (int i = 0; i < whiteCount; i++)
        {
            row = (byte)(compressed[i + 3] >> 5);
            col = (byte)((compressed[i + 3] >> 2) - (row << 3));
            type = (byte)((compressed[i + 3] - (col << 2) - (row << 5)) << 1);
            type += (byte)((compressed[3 + whiteCount - 1 + (i > 7 ? 2 : 1)] >> (7 - (i % 8))) % 2);

            white.Add(new Piece(Piece.ByteToType(type), row, col, true));
            white.Last().hasMoved = true;
        }

        int whiteOffset = white.Count + (white.Count > 8 ? 2 : 1);

        for (int i = 0; i < blackCount; i++)
        {
            row = (byte)(compressed[i + 3 + whiteOffset] >> 5);
            col = (byte)((compressed[i + 3 + whiteOffset] >> 2) - (row << 3));
            type = (byte)((compressed[i + 3 + whiteOffset] - (col << 2) - (row << 5)) << 1);
            type += (byte)((compressed[3 + whiteOffset + blackCount - 1 + (i > 7 ? 2 : 1)] >> (7 - (i % 8))) % 2);

            black.Add(new Piece(Piece.ByteToType(type), row, col, false));
            black.Last().hasMoved = true;
        }

        bool wa = compressed[2] >> 7 % 2 == 1;
        bool wb = compressed[2] >> 6 % 2 == 1;
        bool wc = compressed[2] >> 5 % 2 == 1;
        bool ba = compressed[2] >> 4 % 2 == 1;
        bool bb = compressed[2] >> 3 % 2 == 1;
        bool bc = compressed[2] >> 2 % 2 == 1;

        foreach (Piece p in white)
        {
            if ((!wa || !wc) && p.type == Piece.PieceType.Rook)
            {
                if (p.Col == 0 && p.Row == 0)
                    p.hasMoved = wa;

                if (p.Col == 7 && p.Row == 0)
                    p.hasMoved = wc;
            }
            else if (p.type == Piece.PieceType.King)
            {
                p.hasMoved = wb;
            }
        }

        foreach (Piece p in black)
        {
            if ((!ba || !bc) && p.type == Piece.PieceType.Rook)
            {
                if (p.Col == 0 && p.Row == 7)
                    p.hasMoved = ba;

                if (p.Col == 7 && p.Row == 7)
                    p.hasMoved = bc;
            }
            else if (p.type == Piece.PieceType.King)
            {
                p.hasMoved = bb;
            }
        }
    }

    public static bool Compare(in byte[] first, in byte[] second)
    {
        // Check all the bytes
        for (int i = 0; i < first.Length; i++)
        {
            if (first[i] != second[i])
                return false;
        }

        // All bits are the same
        return true;
    }

    public override string ToString()
    {
        string ret = "  | a | b | c | d | e | f | g | h | \n";
        ret += new string('-', 35) + '\n';
        for (byte i = 7; i <= 7; i--)
        {
            ret += ((char)(i + '1')) + " ";
            for (byte j = 0; j < 8; j++)
            {
                ret += "|";
                Piece p = GetPiece(i, j);
                if (p == null)
                {
                    ret += "   ";
                }
                else
                {
                    ret += " " + (char)p.type + " ";
                }
            }
            ret += "|\n" + new string('-', 35) + '\n';
        }
        return ret;
    }
}