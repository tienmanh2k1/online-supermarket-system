using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;

class Program
{
    static void Main()
    {
        var type = typeof(MySQLMigrationsSqlGenerator);
        var method = type.GetMethod("ColumnDefinitionWithCharSet", BindingFlags.NonPublic | BindingFlags.Instance);
        Console.WriteLine(method == null ? "METHOD NOT FOUND" : "METHOD FOUND");
        if (method != null)
        {
            var body = method.GetMethodBody();
            Console.WriteLine("MaxStackSize: " + body.MaxStackSize);
        }
    }
}
