using Npgsql;

public class Injections
{
    private static readonly string ConnectionString =
        "Host=localhost;Database=bankdb;Username=postgres;Password=password";

    public static void Run()
    {
        SetupDatabase();

        while (true)
        {
            Console.WriteLine("\n=== Banking System ===");
            Console.WriteLine("1. Login");
            Console.WriteLine("2. Transfer Money");
            Console.WriteLine("3. Exit");
            Console.Write("Choose option: ");

            string choice = Console.ReadLine()!;

            switch (choice)
            {
                case "1":
                    Login();
                    break;
                case "2":
                    TransferMoney();
                    break;
                case "3":
                    return;
            }
        }
    }

    static void SetupDatabase()
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        string dropTables =
            @"
            DROP TABLE IF EXISTS transactions;
            DROP TABLE IF EXISTS accounts;";

        using (var cmd = new NpgsqlCommand(dropTables, conn))
        {
            cmd.ExecuteNonQuery();
        }

        string createTables =
            @"
            CREATE TABLE accounts (
                account_id SERIAL PRIMARY KEY,
                username VARCHAR(50),
                password VARCHAR(50),
                balance DECIMAL(10,2)
            );

            CREATE TABLE transactions (
                transaction_id SERIAL PRIMARY KEY,
                from_account INT REFERENCES accounts(account_id),
                to_account INT REFERENCES accounts(account_id),
                amount DECIMAL(10,2),
                description TEXT,
                transaction_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );";

        using (var cmd = new NpgsqlCommand(createTables, conn))
        {
            cmd.ExecuteNonQuery();
        }

        string insertData =
            @"
            INSERT INTO accounts (username, password, balance) VALUES
            ('alice', 'pass123', 1000.00),
            ('bob', 'pass456', 500.00),
            ('admin', 'admin123', 9999.99);

            INSERT INTO transactions (from_account, to_account, amount, description) VALUES
            (1, 2, 100.00, 'Rent payment'),
            (2, 1, 50.00, 'Dinner split'),
            (3, 1, 250.00, 'Bonus payment');";

        using (var cmd = new NpgsqlCommand(insertData, conn))
        {
            cmd.ExecuteNonQuery();
        }
    }

    static void Login()
    {
        Console.Write("Username: ");
        string username = Console.ReadLine()!;
        Console.Write("Password: ");
        string password = Console.ReadLine()!;

        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();

        // på username:
        // a'; DROP TABLE transactions;--

        // på username:
        // admin' --
        string query =
            $"SELECT * FROM accounts WHERE username = '{username}' AND password = '{password}'";

        // SELECT * FROM accounts WHERE username = 'admin'

        using var cmd = new NpgsqlCommand(query, conn);
        try
        {
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                Console.WriteLine($"\nLogged in successfully!");

                // Hämta ut rad+kolumn baserat på kolumnens namn och [] syntax
                Console.WriteLine($"Account ID: {reader["account_id"]}");
                Console.WriteLine($"Balance: ${reader["balance"]}");
            }
            else
            {
                Console.WriteLine("Login failed.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static void TransferMoney()
    {
        Console.Write("From Account ID: ");
        string fromAccount = Console.ReadLine()!;
        Console.Write("To Account ID: ");
        string toAccount = Console.ReadLine()!;
        Console.Write("Amount: $");
        string amount = Console.ReadLine()!;

        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        using var transaction = conn.BeginTransaction();

        try
        {
            string debitQuery =
                $@"
                UPDATE accounts 
                SET balance = balance - {amount} 
                WHERE account_id = {fromAccount} AND balance >= {amount}";

            string creditQuery =
                $@"
                UPDATE accounts 
                SET balance = balance + {amount} 
                WHERE account_id = {toAccount}";

            string transactionQuery =
                $@"
                INSERT INTO transactions (from_account, to_account, amount, description)
                VALUES ({fromAccount}, {toAccount}, {amount}, 'Transfer')";

            Console.WriteLine(debitQuery);
            Console.WriteLine();
            Console.WriteLine(creditQuery);
            Console.WriteLine();
            Console.WriteLine(transactionQuery);


            using (var cmd = new NpgsqlCommand(debitQuery, conn, transaction))
            {
                if (cmd.ExecuteNonQuery() != 1)
                    throw new Exception("Insufficient funds or invalid source account");
            }

            using (var cmd = new NpgsqlCommand(creditQuery, conn, transaction))
            {
                if (cmd.ExecuteNonQuery() != 1)
                    throw new Exception("Invalid destination account");
            }

            using (var cmd = new NpgsqlCommand(transactionQuery, conn, transaction))
            {
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
            Console.WriteLine("Transfer completed successfully!");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"Transfer failed: {ex.Message}");
        }
    }
}
