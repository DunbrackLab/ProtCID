# Protein common interface databases(ProtCID)

ProtCID contains comprehensive, PDB-wide structural information on the interactions of proteins and individual protein domains with other molecules, including four types of interactions: chain interfaces, Pfam domain interfaces, Pfam-peptide interfaces and Pfam-ligand/nucleic acids interactions. A common interaction here indicates chain-chain or Pfam domain-domain interfaces that occur in different crystal forms or Pfam-peptide or Pfam-ligand interactions that occur in multiple homologous proteins.

Its main goal is to identify and cluster homodimeric and heterodimeric interfaces observed in multiple crystal forms of homologous proteins, and interactions of peptide and ligands in homologous proteins. Such interfaces and interactions, especially of non-identical proteins or protein complexes, have been associated with biologically relevant interactions. For more details about the algorithm and benchmarking, please refer to our paper "Statistical Analysis of Interface Similarity in Crystals of Homologous Proteins." and the [ProtCID web site](http://dunbrack2.fccc.edu/ProtCiD).

## Repo Contents

- **AuxFuncLib**
The library contains helper functions: zip or unzip files, parse or write PDB files, run linux programs remotely, format SQL query string, a lot more. 
- **BuCompLib**
The library is used to process biological assemblies from PDB and PISA, including chain interfaces, domain interfaces, chain-peptide interfaces, chain-ligand interactions, and comparison of PDB and PISA biological assemblies of PDB entries. This library is used to generate a firebird database named bucomp.fdb.
- **BuQueryLib**
The library is to query biological assemblies from bucomp.fdb database, and format biological assemblies in four formats: ABC format (e.g. PDB: 3FYU, A3B2C, the chain with maximum copies is always named “A”), entity format (e.g. (1.1)(2.2)(3.3) in the entity ID order in the PDB entry file), asymmetric chain format (e.g. (A)(B, D)(C, E, F) in the entity ID order in the PDB entry file), author chain format (e.g. (A)(B, D)(C, E, F) in the entity ID order in the PDB entry file). 
- **CrystalInterfaceLib**
This is one of main library. It contains all source code to generate interfaces from crystal structures, including build 3x3x3 unit cells from PDB asymmetric unit files, compute interfaces by K-DOPs algorithm, calculate similarity scores and surface area values. 
- **DataCollectorLib**
The library is to generate all sequence and structural alignments at chain and Pfam domain levels, including FatCat structural alignments (http://fatcat.sanfordburnham.org/), HH alignments from HHsuite (https://github.com/soedinglab/hh-suite), and sequence alignments from PsiBlast (ftp://ftp.ncbi.nlm.nih.gov/blast/executables/blast+/LATEST/).  
- **DBLib**
The library contains all operations on ProtCID databases, including database connection, table creation, and query/update/delete/insert data. 
- **InterfaceClusterLib**
This is one of main library. It contains all source code to cluster interfaces, including classify interfaces to homologous groups at chain level and domain level (Pfam),  cluster interfaces, compile all coordinate files of clusters, and generate all PyMol scripts.
- **ProgressLib**
The progress library is to provide a progress bar and window to show the progress of data processing. 
- **ProtCidSettingsLib**
This library is to set and get all directory settings and parameter settings.
- **ProtCidWebDataLib**
This library is to generate meta data for ProtCID web site to speed up web queries.
- **Main program**
The main window form is for all settings, all functions to build ProtCID databases, and progress bar and progress window. However, it is not feasible just click menu items to rebuild ProtCID databases on the entire PDB database and many other data sources used to build ProtCID. ProtCID contains hundreds GBs databases and millions of interface files, cluster files and text files. That is why we provide a web site http://dunbrack2.fccc.edu/protcid, so users can query on our database. 


