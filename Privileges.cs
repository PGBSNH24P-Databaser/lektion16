using Npgsql;

public class Privileges
{
    static readonly string AdminConn =
        "Host=localhost;Database=hospital_db;Username=postgres;Password=password";

    public static void Run()
    {
        SetupDatabase();
        DemonstratePrivilegeEscalation();
    }

    static void SetupDatabase()
    {
        using var conn = new NpgsqlConnection(AdminConn);
        conn.Open();

        ExecuteCommand(
            conn,
            @"
            CREATE ROLE doctor_role;
            CREATE ROLE nurse_role;
            CREATE ROLE intern_role;
            
            -- Create test user with intern role
            CREATE USER evil_intern WITH PASSWORD 'intern123';
            GRANT intern_role TO evil_intern;           
        "
        );

        ExecuteCommand(
            conn,
            @"            
            CREATE TABLE patients (
                id SERIAL PRIMARY KEY,
                name VARCHAR(100),
                diagnosis VARCHAR(200),
                ssn VARCHAR(11),
                insurance_id VARCHAR(20)
            );
            
            CREATE TABLE medications (
                id SERIAL PRIMARY KEY,
                patient_id INTEGER REFERENCES patients(id),
                medication VARCHAR(100),
                dosage VARCHAR(50),
                prescribed_by VARCHAR(100)
            );
            
            CREATE TABLE audit_log (
                id SERIAL PRIMARY KEY,
                action_type VARCHAR(50),
                table_name VARCHAR(50),
                user_role VARCHAR(50),
                action_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
        "
        );

        ExecuteCommand(
            conn,
            @"
            -- Doctor privileges
            GRANT SELECT, INSERT, UPDATE ON patients TO doctor_role;
            GRANT SELECT, INSERT, UPDATE, DELETE ON medications TO doctor_role;
            GRANT SELECT ON audit_log TO doctor_role;

            -- Nurse privileges
            GRANT SELECT ON patients TO nurse_role;
            GRANT SELECT, UPDATE ON medications TO nurse_role;
            GRANT SELECT ON audit_log TO nurse_role;

            -- Intern privileges (too permissive!)
            GRANT SELECT, INSERT ON patients TO intern_role;
            GRANT SELECT ON medications TO intern_role;
            GRANT SELECT, INSERT, UPDATE ON audit_log TO intern_role;
        "
        );

        // Insert sample data
        ExecuteCommand(
            conn,
            @"
            INSERT INTO patients (name, diagnosis, ssn, insurance_id) VALUES
            ('John Doe', 'Hypertension', '123-45-6789', 'INS001'),
            ('Jane Smith', 'Diabetes', '987-65-4321', 'INS002');

            INSERT INTO medications (patient_id, medication, dosage, prescribed_by) VALUES
            (1, 'Lisinopril', '10mg daily', 'Dr. Wilson'),
            (2, 'Metformin', '500mg twice daily', 'Dr. Brown');
        "
        );
    }

    static void DemonstratePrivilegeEscalation()
    {
        Console.WriteLine("=== Demonstrating Privilege Escalation Risks ===\n");

        // Connect as evil_intern
        string internConn =
            "Host=localhost;Database=hospital_db;Username=evil_intern;Password=intern123";
        using var conn = new NpgsqlConnection(internConn);
        conn.Open();

        Console.WriteLine("1. Normal Access (Intended Usage):");
        ExecuteAndPrint(conn, "SELECT name, diagnosis FROM patients;");

        Console.WriteLine("\n2. Inserting Bad Data (Exploit 1):");
        ExecuteAndPrint(
            conn,
            @"
            -- Intern can, intentionally or by mistake, insert bad data
            INSERT INTO patients (id, name, diagnosis, ssn, insurance_id) VALUES
                (56, 'Blablabla', 'idk', 'rh4983hf+92', 'def-not-an-ins-id');
        "
        );

        Console.WriteLine("\n3. Privilege Escalation via Audit Log (Exploit 2):");
        ExecuteAndPrint(
            conn,
            @"
            -- Intern can modify audit log to cover tracks
            UPDATE audit_log 
            SET action_type = 'AUTHORIZED_ACCESS'
            WHERE user_role = 'intern_role';
        "
        );
    }

    static void ExecuteCommand(NpgsqlConnection conn, string sql)
    {
        using var cmd = new NpgsqlCommand(sql, conn);
        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing command: {ex.Message}");
        }
    }

    static void ExecuteAndPrint(NpgsqlConnection conn, string sql)
    {
        Console.WriteLine($"Executing: {sql}\n");
        using var cmd = new NpgsqlCommand(sql, conn);
        try
        {
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Console.WriteLine("-");
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    Console.WriteLine($"  {reader.GetName(i)}: {reader[i]}\t");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
}
