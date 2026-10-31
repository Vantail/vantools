namespace VanTools.Core;

public interface ICommand {
    string Name { get; }
    string Description { get; }
    int Execute(string[] args);
}
