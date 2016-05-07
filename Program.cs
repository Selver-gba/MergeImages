using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace MergeImages
{
    public class ProgramOptions : IDisposable
    {
        private const int c_SectorSize = 512;

        #region // Private readonly fields
        private readonly System.IO.FileInfo m_OutfileName;
        private readonly System.IO.FileInfo m_LogfileName;
        private readonly System.Collections.Immutable.ImmutableArray<System.IO.FileInfo> m_InputFileNames;

        private readonly System.IO.FileStream m_Out;
        private readonly System.IO.StreamWriter m_Log;
        private readonly System.Collections.Immutable.ImmutableArray<System.IO.FileStream> m_In;
        #endregion // Private readonly fields

        #region // Public properties
        public int SectorSize { get; } = c_SectorSize;
        public System.IO.FileInfo OutfileName { get { return m_OutfileName; } }
        public System.IO.FileInfo LogfileName { get { return m_LogfileName; } }
        public System.Collections.Immutable.ImmutableArray<System.IO.FileInfo> InputFileNames { get { return m_InputFileNames; } }

        public System.IO.FileStream OutStream { get { return m_Out; } }
        public System.IO.StreamWriter LogStream { get { return m_Log; } }
        public System.Collections.Immutable.ImmutableArray<System.IO.FileStream> InStreams { get { return m_In; } }
        #endregion // Public properties

        private ProgramOptions(
            System.IO.FileInfo outfileName
            , System.IO.FileInfo logfileName
            , System.IO.FileInfo[] inputFileNames
            , System.IO.FileStream outfile
            , System.IO.StreamWriter logfile
            , System.IO.FileStream[] infiles
            )
        {
            m_OutfileName = outfileName;
            m_LogfileName = logfileName;
            m_InputFileNames = System.Collections.Immutable.ImmutableArray.Create<System.IO.FileInfo>(inputFileNames);
            m_Out = outfile;
            m_Log = logfile;
            m_In = System.Collections.Immutable.ImmutableArray.Create<System.IO.FileStream>(infiles);

            #region // Invariants
            if (null == m_OutfileName) { throw new ArgumentNullException(nameof(outfileName)); }
            if (null == m_LogfileName) { throw new ArgumentNullException(nameof(logfileName)); }
            if (null == m_InputFileNames) { throw new ArgumentNullException(nameof(inputFileNames)); }
            if (null == m_Out) { throw new ArgumentNullException(nameof(outfile)); }
            if (null == m_Log) { throw new ArgumentNullException(nameof(logfile)); }
            if (null == m_In) { throw new ArgumentNullException(nameof(infiles)); }
            if (m_InputFileNames.Length < 2) { throw new Exception("Need at least two input files"); }
            if (m_In.Length != m_InputFileNames.Length) { throw new Exception("mismatched number of input files (internal error)"); }
            if (m_InputFileNames.Any(p => null == p)) { throw new ArgumentException("All input files must be non-null (internal error)"); }
            if (m_In.Any(p => null == p)) { throw new ArgumentException("All input files must be non-null (internal error)"); }
            if (m_InputFileNames.Any(p => 0 != (p.Length % c_SectorSize))) { throw new ArgumentException($"All fileInfo must show length as multiple of {c_SectorSize} bytes"); }
            if (m_In.Any(p => 0 != (p.Length % c_SectorSize))) { throw new ArgumentException($"All input streams must show length as multiple of {c_SectorSize} bytes"); }

            var commonSize = m_In.First().Length;
            if (m_InputFileNames.Any(p => commonSize != p.Length)) { throw new ArgumentException($"All fileInfo must show length as {commonSize} bytes"); }
            if (m_In.Any(p => commonSize != p.Length)) { throw new ArgumentException($"All input streams must show length as {commonSize} bytes"); }
            #endregion // Invariants
        }

        /// <summary>
        /// Initializes options from commmand line.  Throws an exception on errors.
        /// </summary>
        /// <param name="args">the parameters for the program</param>
        /// <returns>Object having all options for running the program</returns>
        public static ProgramOptions Initialize(string[] args)
        {
            if (null == args) { throw new ArgumentNullException("No arguments provided."); }
            if (3 > args.Length) { throw new ArgumentException("Need at least three arguments."); }

            var outfilename = new System.IO.FileInfo(args[0]);
            var logfilename = new System.IO.FileInfo(outfilename.FullName + ".log");
            var infilenames = new System.IO.FileInfo[args.Length - 1];
            for (int i = 1; i < args.Length; i++)
            {
                infilenames[i - 1] = new System.IO.FileInfo(args[i]);
            }
            #region // Verify output / logfile directory exists, but files don't exist
            if (!outfilename.Directory.Exists)
            {
                throw new ArgumentException($"Output directory does not exist ({outfilename.DirectoryName})");
            }
            if (outfilename.Exists)
            {
                throw new ArgumentException($"Output file already exists ({outfilename.FullName})");
            }
            if (logfilename.Exists)
            {
                throw new ArgumentException($"Output file already exists ({logfilename.FullName})");
            }
            #endregion // Verify output / logfile directory exists, but files don't exist

            #region // Verify all input files exist
            for (int i = 0; i < infilenames.Length; i++)
            {
                if (!infilenames[i].Exists) { throw new ArgumentException($"Input file does not exist ({infilenames[i].FullName})"); }
            }
            #endregion // Verify all input files exist

            #region // open output file and log file
            var outStream = new System.IO.FileStream(
                outfilename.FullName // path
                , System.IO.FileMode.CreateNew
                , System.IO.FileAccess.ReadWrite
                , System.IO.FileShare.None
                , 64 * 1024 // bufferSize for lower-level IO (FileStream hides details of IO buffering)
                , System.IO.FileOptions.SequentialScan
                );
            var logStream = new System.IO.FileStream(
                logfilename.FullName // path
                , System.IO.FileMode.CreateNew
                , System.IO.FileAccess.ReadWrite
                , System.IO.FileShare.None
                , 64 * 1024 // bufferSize for lower-level IO (FileStream hides details of IO buffering)
                , System.IO.FileOptions.SequentialScan
                );
            var logStreamWriter = new System.IO.StreamWriter(
                logStream
                , System.Text.Encoding.UTF8
                , 64*1024
                , false
                );
            #endregion // open output file and log file

            #region // open all the input files for reading
            var infiles = new System.IO.FileStream[infilenames.Length];
            for (int i = 0; i < infiles.Length; i++)
            {
                infiles[i] = new System.IO.FileStream(
                    infilenames[i].FullName
                    , System.IO.FileMode.Open
                    , System.IO.FileAccess.Read
                    , System.IO.FileShare.Read
                    , 64* 1024 // bufferSize for lower-level IO (FileStream hides details of IO buffering)
                    , System.IO.FileOptions.SequentialScan
                    );
            }
            #endregion // open all the input files for reading

            var options = new ProgramOptions(outfilename, logfilename, infilenames, outStream, logStreamWriter, infiles);

            return options;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    m_Out?.Dispose();
                    m_Log?.Dispose();
                    if (null != m_In)
                    {
                        for (int i = 0; i < m_In.Length; i++)
                        {
                            m_In[i]?.Dispose();
							m_In[i] = null;
                        }
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ProgramOptions() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }

    enum SectorState
    {
        Unknown = 0,
        AllZero = 1,
        AllFF   = 2,
        RepeatedNonzeroNonFFValue = 3,
        Reasonable = -1,
    }
    class Program
    {
        static void ShowHelp()
        {			
            Console.WriteLine();
            Console.WriteLine("Usage: MergeImages.exe  <outfile> <file1> <file2> [file3 [...]]");
            Console.WriteLine("\t{0,10} {1}", "outfile", "The file to create with merged data");
            Console.WriteLine("\t{0,10} {1}", "file1",   "First   file to binary merge");
            Console.WriteLine("\t{0,10} {1}", "file2",   "Second  file to binary merge");
            Console.WriteLine("\t{0,10} {1}", "file3",   "Third   file to binary merge");
            Console.WriteLine("\t{0,10} {1}", "...",     "more... files to binary merge");
            Console.WriteLine();
            Console.WriteLine("Requirements/Limitations:");
            Console.WriteLine("\tAll files must be multiple of 512 bytes.");
            Console.WriteLine("\tAll files must be the same size");
            Console.WriteLine("\tAll files must be capable of being opened read-only (shared read)");
            Console.WriteLine("\tData analysis done on 512 bytes boundaries only.");
            Console.WriteLine();
            Console.WriteLine("The following data is considered suspect, and will be logged:");
            Console.WriteLine("\tRepeated non-zero bytes");
            Console.WriteLine("\tData that differs between the files");
            Console.WriteLine("");
            Console.WriteLine("With only two file:");
            Console.WriteLine("\tIf the sector data matches, it is accepted");
            Console.WriteLine("\tElse, filled with repeated 0xDE 0xAD bytes");
            Console.WriteLine("");
            Console.WriteLine("With 3+ files:");
            Console.WriteLine("\tMajority (2+) wins for non-suspect, non-zero data");
            Console.WriteLine("\tElse majority (2+) wins for zero data");
            Console.WriteLine("\tElse, filled with repeated 0xDE 0xAD bytes");
            Console.WriteLine("");
            Console.WriteLine("Logfile will equal outfile with .log extension appended");
            Console.WriteLine("");
        }
        
        /// <summary>
        /// The purpose of this utility is to merge multiple large binary
        /// objects semi-automatically, and to log detected issues for
        /// later manual review.
        /// </summary>
        /// <param name="args">the argument list for the program</param>
        static void Main(string[] args)
        {
            ProgramOptions options = null;

            if (System.Diagnostics.Debugger.IsAttached && (0 == args.Length))
            {
				// this just 
                bool overrideArguments = true;
                System.Diagnostics.Debugger.Break();
                if (overrideArguments)
                {
                    args = new String[5]
                    {
                     @"Z:\3DSBackups\N3DS_v10_03_Patched.img"
                    ,@"Z:\3DSBackups\N3DS_v10_03_Try1.img"
                    ,@"Z:\3DSBackups\N3DS_v10_03_Try2.img"
                    ,@"Z:\3DSBackups\N3DS_v10_03_Try3.img"
                    ,@"Z:\3DSBackups\N3DS_v10_03_Try4.img"
                    };
                }
            }

            try
            {
                options = ProgramOptions.Initialize(args);
            }
            catch (Exception e)
            {
                Console.WriteLine("");
                Console.WriteLine("Exception parsing command line arguments:");
                Console.WriteLine(e.Message);
                Console.WriteLine("");
                ShowHelp();
                options = null;
                return;
            }

            PerformMerge(options);



            Console.WriteLine("Succeeded!");

        }

        /// <summary>
        /// What is the state of a single encrypted sector?
        /// This just checks for common errors, such as all-0xFF data.
		/// Also checks for an error at the end of the sector, such as
		/// where the data simply stopped being sent mid-sector, relying
		/// on the fact that the data is supposedly encrypted, and thus
		/// should be randomized data.
        /// </summary>
        /// <param name="buffer">a 512-byte buffer corresponding to a single sector of data</param>
        private static SectorState DetermineSectorState(byte[] buffer)
        {
            if (buffer.Length < 512) return SectorState.Unknown;
            if (buffer.All(p => p == 0x00)) return SectorState.AllZero;
            if (buffer.All(p => p == 0xFF)) return SectorState.AllFF;

            byte suspectCharacter = buffer[buffer.Length - 1];
            int count = 0;
            int i = buffer.Length - 1;
            while ((i > 0) && (buffer[i] == suspectCharacter))
            {
                count++;
                i--;
            }
            if (count > 0x10) // remember, these are supposed to be encrypted sectors!
            {
                return SectorState.RepeatedNonzeroNonFFValue;
            }
            return SectorState.Reasonable;
        }

        private static void PerformMerge(ProgramOptions options)
        {
            #region // prep a single buffer to correspond to no automatically determined data
            byte[] badData = new byte[options.SectorSize];
            for (int i = 0; i < options.SectorSize; i+=2)
            {
                badData[i + 0] = 0xDE;
                badData[i + 1] = 0xAD;
            }
            #endregion // prep a single buffer to correspond to no automatically determined data

            #region // how many buffers are needed?  One per input file and one for output
            byte[] outbuffer = new byte[options.SectorSize];
            byte[][] inBuffers = new byte[options.InStreams.Length][];
            SectorState[] state = new SectorState[options.InStreams.Length];
            for (int i = 0; i < options.InStreams.Length; i++)
            {
                state[i] = SectorState.Unknown;
                inBuffers[i] = new byte[options.SectorSize];
            }
			#endregion // how many buffers are needed?  One per input file and one for output

            long numberOfSectors = options.InStreams.First().Length / options.SectorSize;
            for (long sector = 0; sector < numberOfSectors; sector++)
            {
                if (0 == (sector % 0x100))
                {
                    Console.Write("\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b");
                    float percentageComplete = sector;
                    percentageComplete /= numberOfSectors;
                    percentageComplete *= 100;

                    Console.Write("{0,8} / {1}  {2:0.0}%", sector, numberOfSectors, percentageComplete);
                }




                #region // Reset sector state
                for (int i = 0; i < options.InStreams.Length; i++)
                {
                    state[i] = SectorState.Unknown;
                }
                #endregion // Reset sector state
                #region // read all the input files and determine each file's sector state for that sector
                for (int i = 0; i < options.InStreams.Length; i++)
                {
                    var s = options.InStreams[i];
                    var b = inBuffers[i];
                    var bytesRead = s.Read(b, 0, options.SectorSize);
                    // N.B. Technically, implementation could return partial results.  Don't both handling that edge case here.
                    if (bytesRead != options.SectorSize)
                    {
                        throw new Exception($"Failed to read sector {sector} for file {options.InputFileNames[i]}");
                    }

                    state[i] = DetermineSectorState(b);
                }
                #endregion // read all the input files and determine each file's sector state for that sector

                int preferredSource = -1;
                #region // short-circuit if all sources show all-FF or all-zero
                if (state.All(p => SectorState.AllZero == p))
                {
					// This happens a *LOT*... so don't log it here.
                    // options.LogStream.Write($"1100: Sector {0,-7} ({1:00000000x}) File 0 selected (all sources are all 0x00 data)\r\n", sector, sector*options.SectorSize);
                    preferredSource = 0;
                }
                else if (state.All(p => SectorState.AllFF == p))
                {
					// This happens a *LOT*... so don't log it here.
                    //options.LogStream.Write("1200: Sector {0,-7} ({1:00000000x}) File 0 selected (all sources are all 0xFF data)\r\n", sector, sector * options.SectorSize);
                    preferredSource = 0;
                }
                #endregion // short-circuit if all sources show all-FF or all-zero

                #region // find a preferred source having reasonable data
                for (int i = 0; (-1 == preferredSource) && (i < options.InStreams.Length); i++ )
                {
                    if (state[i] != SectorState.Reasonable) { continue; }

                    int matches = 0;
                    #region // Determine number of matches for this potentially reasonable data
                    var src = inBuffers[i];
                    for (int j = 0; j < options.InStreams.Length; j++)
                    {
                        if (i == j) { matches++; continue; }
                        var dst = inBuffers[j];
                        var result = src.SequenceEqual(dst);
                        if (src.SequenceEqual(dst)) { matches++; }
                    }
                    #endregion // Determine number of matches for this potentially reasonable data
                    #region // Determine if sufficient matches to make this the preferred source
                    if (options.InStreams.Length == matches)
                    {
                        preferredSource = i;
                    }
                    else if ((options.InStreams.Length / 2) < matches)
                    {
                        options.LogStream.Write("1300: Sector {0,-7} ({1:00000000x}) File {2} matched {3} other reasonable data (preferred selection)\r\n", sector, sector * options.SectorSize, i, matches);
                        preferredSource = i;
                    }
                    else
                    {
                        options.LogStream.Write("1400: Sector {0,-7} ({1:00000000x}) File {2} matched {3} other reasonable data (not preferred)\r\n", sector, sector * options.SectorSize, i, matches);
                    }
                    #endregion // Determine if sufficient matches to make this the preferred source
                }
                #endregion // find a preferred source having reasonable data

                #region // Special-case: If only two input files, and one reasonable and the other not reasonable, use reasonable one
                if ((-1 == preferredSource) && (options.InStreams.Length == 2))
                {
                    if ((SectorState.Reasonable == state[0]) && (SectorState.RepeatedNonzeroNonFFValue == state[1]))
                    {
                        preferredSource = 0;
                        options.LogStream.Write("2100: Sector {0,-7} ({1:00000000x}) File 0 selected (2 files, one reasonable, one suspect)\r\n", sector, sector * options.SectorSize);
                    }
                    else if ((SectorState.Reasonable == state[1]) && (SectorState.RepeatedNonzeroNonFFValue == state[0]))
                    {
                        preferredSource = 1;
                        options.LogStream.Write("2200: Sector {0,-7} ({1:00000000x}) File 1 selected (2 files, one reasonable, one suspect)\r\n", sector, sector * options.SectorSize);
                    }
                }
                #endregion // Special-case: If only two input files, and one reasonable and the other not reasonable, use reasonable one

                #region // All dumps may be suspect, but contents are equal, it's actually OK
                if ((-1 == preferredSource) && (state.All(p => SectorState.RepeatedNonzeroNonFFValue == p)))
                {
                    bool mismatchFound = false;
                    var src = inBuffers[0];
                    for (int i = 1; i < state.Length; i++)
                    {
                        if (!src.SequenceEqual(inBuffers[i]))
                        {
                            mismatchFound = true;
                        }
                    }
                    if (mismatchFound)
                    {
                        options.LogStream.Write("3100: Sector {0,-7} ({1:00000000x}) mismatched suspect repeated data\r\n", sector, sector * options.SectorSize);
                    }
                    else
                    {
                        preferredSource = 0;
                    }
                }
                #endregion // All dumps may be suspect, but contents are equal, it's actually OK

                #region // Select the buffer to write
                byte[] finalBuffer = null;
                if (-1 == preferredSource)
                {
                    options.LogStream.Write($"4000: Sector {0,-7} ({1:00000000x}) -- No automatically selectable data found\n", sector, sector * options.SectorSize);
                    finalBuffer = badData;
                }
                else
                {
                    finalBuffer = inBuffers[preferredSource];
                }
                #endregion // Select the buffer to write
                options.OutStream.Write(finalBuffer, 0, options.SectorSize);
            }

            options.OutStream.Flush();
            options.LogStream.Flush();
            return;
        }
    }
}
