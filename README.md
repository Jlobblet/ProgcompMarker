# ProgcompMarker

This was thrown together in one evening to serve an immediate need.
The project is made of two main parts: the server, and the CLI.
The CLI behaves as follows:
- download input data
- run a provided solution
- send solutions to the server to be checker
- report the result

The server is made with Suave and is extremely barebones.

## Installation

### CLI

Download one of the pre-compiled releases.

### Server

Clone and run with `dotnet run --release`.

## Usage

### CLI

Set the `PROGCOMP_USER` environment variable to your username.
This should be consistent with yourself.
The server performs no validation on it to check that you don't change it or have a clash, so pick something reasonably unique.

For invoking the command itself, you need to provide the problem number to attempt and the path to your solution.
The solution should be an executable file (either binary or a script with appropriate permissions) so that the CLI can run it for you.
`./CLI [PROBLEM NUMBER] [PATH TO EXECUTABLE]`.

You may wish to first `export PROGCOMP_USER=jlb` or set it each time e.g. `PROGCOMP_USER=jlb ./CLI 1 solution1`.
