using System;
using System.Reflection;
using MediaBrowser.Controller.Library;

namespace TestIntrospect
{
    class Program
    {
        static void Main(string[] args)
        {
            var type = typeof(IUserManager);
            Console.WriteLine($"\nInspecting IUserManager: {type.FullName}");
            
            Console.WriteLine("\nProperties:");
            foreach (var p in type.GetProperties())
            {
                 Console.WriteLine($" - {p.Name} ({p.PropertyType})");
            }

            Console.WriteLine("\nGetUserById Signature:");
            var method = type.GetMethod("GetUserById");
            if (method != null)
            {
                Console.WriteLine($" - Return: {method.ReturnType}");
                foreach (var p in method.GetParameters())
                {
                    Console.WriteLine($" - Param: {p.Name} ({p.ParameterType})");
                }
            }
        }
    }
}
