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

### ProgcompCli

Download one of the pre-compiled releases.

### Server

Clone and run with `dotnet run -c Release`.

## Usage

### ProgcompCli

Set the `PROGCOMP_ENDPOINT` environment variable to `https://progcomp.uwcs.co.uk/`.

Set the `PROGCOMP_USERNAME` environment variable to your username.
This should be consistent with yourself.
The server performs no validation on it to check that you don't change it or have a clash, so pick something reasonably unique.

For invoking the command itself, you need to provide the problem number to attempt and the path to your solution.
The solution should be an executable file (either binary or a script with appropriate permissions) so that the CLI can run it for you.
`./ProgcompCli [PROBLEM NUMBER] [PATH TO EXECUTABLE]`.

You may wish to first `export PROGCOMP_USERNAME=jlb` or set it each time e.g. `PROGCOMP_USERNAME=jlb ./ProgcompCli 1 solution1`.

### Server

Add the problem inputs and outputs to the `data` directory in the server's working directory. The structure should be:
```
data
└── 1
    ├── answers.txt
    └── inputs.txt
```
and repeat for each problem.
Problems must have integral names for now (bounded by 64-bit unsigned integer which should be more than enough).
