namespace VanTools.Core;

public interface ICommand
{
    string Name { get; }
    int Execute(string[] args);
}
