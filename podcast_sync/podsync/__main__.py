import sys

args = sys.argv[1:]
if args and (args[0] == "--cli" or any(not a.startswith("-") for a in args) or "-h" in args or "--help" in args
             or "--mode" in args):
    from .app import run_cli
    run_cli([a for a in args if a != "--cli"])
else:
    from .gui import main
    main()
