# Protein common interface databases(ProtCID)

ProtCID contains comprehensive, PDB-wide structural information on the interactions of proteins and individual protein domains with other molecules, including four types of interactions: interfaces between full-length protein chains, Pfam domain/domain interfaces, Pfam domain/peptide interfaces and Pfam domain/ligand interactions (including nucleic acids as ligands). A common interactionn occurs in different crystal forms or in non-crystallographic structures across a family of homologous proteins.

Its main goal is to identify and cluster homodimeric and heterodimeric interfaces observed in multiple crystal forms of homologous proteins, and interactions of peptide and ligands in homologous proteins. Such interfaces and interactions, especially of non-identical proteins or protein complexes, have been associated with biologically relevant interactions. For more details about the algorithm and benchmarking, please refer to our paper "Statistical Analysis of Interface Similarity in Crystals of Homologous Proteins." and the [ProtCID web site](http://dunbrack2.fccc.edu/ProtCiD).

We provide source code and dynamically linked libraries for ProtCID. However, we do not suggest rebuilding the ProtCID databases on the entire PDB database since it contains hundreds of GBs of databases and millions of interface files, cluster files, and text files. We provide a web site http://dunbrack2.fccc.edu/protcid, so users can query on our database.


## Repo Contents

- **AuxFuncLib**
This library contains helper functions to zip or unzip files, parse or write PDB files, run Linux programs remotely, format SQL query string and other functions. 
- **BuCompLib**
This library is used to process biological assemblies from the PDB and PISA, including chain interfaces, domain interfaces, chain-peptide interfaces, chain-ligand interactions, and comparison of PDB and PISA biological assemblies of PDB entries. This library is used to generate a FireBird database named bucomp.fdb.
- **BuQueryLib**
This library is to used to query biological assemblies from bucomp.fdb database, and format the stoichiometry of biological assemblies in four formats: ABC format (e.g. PDB: 3FYU, A3B2C, the chain with maximum copies is always named “A”), entity format (e.g. (1.1)(2.2)(3.3) in the entity ID order in the PDB mmCIF or XML file), asymmetric chain format (asym_id, e.g. (A)(B, D)(C, E, F) in the entity ID order in the PDB entry file), author chain format (e.g. (A)(B, D)(C, E, F) in the entity ID order in the PDB entry file). 
- **CrystalInterfaceLib**
This is one of the main libraries. It contains all source code to generate interfaces from crystal structures, including building 3x3x3 unit cells from PDB asymmetric unit files, computing interfaces by K-DOPs algorithm and calculating similarity scores and surface area values. 
- **DataCollectorLib**
This library is used to generate all sequence and structural alignments at the chain and Pfam domain levels, including [FatCat](http://fatcat.sanfordburnham.org/) structural alignments, HMM-HMM alignments from [HHsuite](https://github.com/soedinglab/hh-suite), and sequence alignments from [PSI-BLAST](https://blast.ncbi.nlm.nih.gov/Blast.cgi?CMD=Web&PAGE_TYPE=BlastDocs&DOC_TYPE=Download).  
- **DBLib**
This library contains all operations on ProtCID databases, including database connection, table creation, and querying/updating/deleting/inserting data. 
- **InterfaceClusterLib**
This is one of the main libraries. It contains all source code to cluster interfaces, including classifying interfaces at the homologous chain level and domain level (Pfam),  clustering interfaces, compiling all coordinate files of clusters, and generating all PyMol scripts.
- **ProgressLib**
The progress library provides a progress bar and window to show the progress of data processing. 
- **ProtCidSettingsLib**
This library is used to set and get all directory and parameter settings.
- **ProtCidWebDataLib**
This library is used to generate meta data for the [ProtCID web site](http://dunbrack2.fccc.edu/protcid/) in order to speed up web queries.
- **Main program**
The main window form contains all menu items of functions to build the whole ProtCID on the entire PDB database and other data sources. 
However, we do not suggest rebuilding the ProtCID databases on the entire PDB database since it contains hundreds of GBs of databases and millions of interface files, cluster files, and text files. It needs third-party software, months of computation time and careful evaluation and manual check. We provide a web site http://dunbrack2.fccc.edu/protcid, so users can query on our database.


