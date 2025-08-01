#!/bin/bash

echo "Adding Swagger/OpenAPI support to OCR Services..."

# Add Swashbuckle package
echo "Installing Swashbuckle.AspNetCore..."
dotnet add package Swashbuckle.AspNetCore

# Create a backup of the current Program.cs
echo "Creating backup of Program.cs..."
cp Program.cs Program.cs.backup

echo "Done! Next steps:"
echo "1. Review Program-with-swagger.cs.example for implementation"
echo "2. Update your Program.cs with Swagger configuration"
echo "3. Run 'dotnet build' to verify everything compiles"
echo "4. Run 'dotnet run' and navigate to https://localhost:[port]/swagger"
echo ""
echo "Your original Program.cs has been backed up to Program.cs.backup"