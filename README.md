# FixDataset
Fix problems with Visual Studio datasets (.xsd).

- Reset SQL eg whem VS rewrites complex WHERE clauses into long winded drivel
- Reset Query type - eg VS changes Insert query that returns new PK from Scalar to NonQuery.
- Reset parameter type/size - eg a non-field-associated parameter that VS screws up.

Usage: FixDataset -xsd \path\to\xsd_file -paramfile <inputfile> | [-adapter <table_adapter_name> -method <method_name> -sql <sql> | -query <querytype> | [-paramname <paramname> -paramtype <paramtype>]] -regen namespace

-xsd: full path to the .xsd file to alter (required).

-paramfile: (optional) a file with one line for each operation using following parameters, or ':' for comment lines.

-adapter: name of tableadapter to modify (required)

-method: name of method to modify (required). Default Methods are called Select,Delete,Update,Insert.

-sql: set the SQL for the method. (VS rewrites WHERE clauses into long winded drivel).

-query: set the querytype to NonQuery/Scalar. Common use: reset an Insert method to Scalar (VS changes to NonQuery).

-paramname: Specify the Method parameter name to alter:

-paramtype: Change a Method DbType to this. String default size is 1024, rest size should be 1. (String/Boolean/Int32/DateTime allowed).

-paramsize: if paramtype is String, specify size. Default is 1024. Ignored for other types.

-regen namespace: (commandline only - not allowed in paramfile) regenerate the .designer.cs file from an altered .xsd file.


