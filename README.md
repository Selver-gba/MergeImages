# MergeImages
Merges multiple (each potentially corrupted) encrypted files into
a single image, choosing the "best data" for each sector.

## Example of where this is useful
I had an experience where I was backing up an encrypted data source.
The data size was large enough (>1GB), that due to a hardware glitch,
there was random corruption with every backup.  However, each backup
would have different sectors corrupted.  Therefore, by creating many
backups, the correct data could be reasonably inferred.

For example, I would create 3 backup files, each with some random
corrupt sectors.  A simple "voting" algorithm can then be used to
choose correct data, so long as a majority of the three files agree
as to the contents.

This is a special-purpose tool, as it presumes the sectors should
contain random data (e.g., encrypted contents), and also special-cases
certain errors that currently appear unique to e-MMC readers.

_Examples_:
* Flash - 0x00 and 0xFF are considered "empty" sectors, and thus skip the randomness check
* Randomness - If the same byte is repeated 0x10 times or more at the end of the sector, it is treated as an error.

Given the size of the data, finding and merging the data by hand was
not a reasonable option.  Thus, I automated it, resulting in this code.

I post it here, in the hope that it can be useful to others.


## Usage:

    MergeImages.exe  <outfile> <file1> <file2> [file3 [...]]
    Parameter | Meaning
    --------- | -------
    outfile   | The file to create with merged data
    file1     | First   file to binary merge
    file2     | Second  file to binary merge
    file3     | Third   file to binary merge
    ...       | more... files to binary merge

Requirements/Limitations:
* All files must be multiple of 512 bytes.
* All files must be the same size
* All files must be capable of being opened read-only (shared read)
* Data analysis done on 512 bytes boundaries only.

The following data is considered **suspect**, and will be logged:
* Repeated non-zero bytes
* Data that differs between the files

With only **two** files:
* If the sector data matches, it is accepted
* Else, filled with repeated 0xDE 0xAD bytes

With **three or more** files:
* Majority (2+) wins for non-suspect, non-zero data
* Else majority (2+) wins for zero data
* Else, filled with repeated 0xDE 0xAD bytes

Logfile will equal outfile with .log extension appended


