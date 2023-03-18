# About

IN DEVELOPMENT!  DO NOT USE!

This is a simple code generator that I'm hacking away at for some personal projects.

C# is a minefield when it comes to memory management it seems.  Specially with structs +

The goal of this project is to provide sufficiently good code generator so we may have
smart-pointer-like objects.  For example, a file would "auto dispose" at end of whatever
scope it's in.

# Warning

This module will alter existing source files.  There is a risk of broken code.

Do use source control, do keep backups.  I accidentally wiped some of my C#
files while developing this tool.


# Features
## IAutoDisposable

Provided a "Disposable" method and if the object derives from IAutoDisposable,
the following will be generated:
* A Dispose(bool) method
* A finalizer calling the Dispose(bool) method
* Code inside Dispose() to suppress the GC and call Dispose(bool)

(trying to follow best practices from https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose)

