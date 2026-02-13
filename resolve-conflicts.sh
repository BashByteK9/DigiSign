#!/bin/bash
# DigiSign Merge Conflict Resolution Script
# For use in Git Bash on Windows

echo "=================================="
echo "DigiSign Conflict Resolution Helper"
echo "=================================="
echo ""

# Check if in merge state
if ! git status | grep -q "both modified"; then
    echo "No merge conflicts detected."
    echo "Either merge is complete or hasn't started."
    exit 0
fi

echo "The following files have conflicts:"
git diff --name-only --diff-filter=U
echo ""

# Function to resolve a file
resolve_file() {
    local file=$1
    local strategy=$2
    
    if [ "$strategy" = "ours" ]; then
        git checkout --ours "$file"
        git add "$file"
        echo "✓ Kept digisign-prod version: $file"
    elif [ "$strategy" = "theirs" ]; then
        git checkout --theirs "$file"
        git add "$file"
        echo "✓ Accepted master version: $file"
    elif [ "$strategy" = "manual" ]; then
        echo "⚠ Manual resolution needed: $file"
        echo "  Please edit the file and resolve conflicts manually."
    fi
}

# Ask for resolution strategy for each conflict
while IFS= read -r file; do
    echo ""
    echo "Conflict in: $file"
    echo ""
    
    # Check if it's a critical signing file
    case "$file" in
        SignatureHelper.cs|DigitalSignatureService.cs|X509Certificate2Extension.cs|SignatureConfiguration.cs)
            echo "🔒 CRITICAL SIGNING FILE - Using digisign-prod version"
            resolve_file "$file" "ours"
            ;;
        packages.config)
            echo "📦 PACKAGE FILE - Using digisign-prod version"
            resolve_file "$file" "ours"
            ;;
        Program.cs)
            echo "⚠️ MAIN PROGRAM - Requires manual review"
            echo "Opening in default editor..."
            start "$file" 2>/dev/null || code "$file" 2>/dev/null || vi "$file"
            echo ""
            read -p "After resolving, press Enter to mark as resolved..."
            git add "$file"
            echo "✓ Marked as resolved: $file"
            ;;
        *)
            echo "Choose resolution strategy:"
            echo "  1) Keep digisign-prod version (--ours)"
            echo "  2) Accept master version (--theirs)"
            echo "  3) Manual merge (edit file)"
            read -p "Choice (1/2/3): " choice
            
            case $choice in
                1) resolve_file "$file" "ours" ;;
                2) resolve_file "$file" "theirs" ;;
                3) 
                    start "$file" 2>/dev/null || code "$file" 2>/dev/null || vi "$file"
                    read -p "After resolving, press Enter to mark as resolved..."
                    git add "$file"
                    echo "✓ Marked as resolved: $file"
                    ;;
                *) 
                    echo "Invalid choice. Skipping $file"
                    ;;
            esac
            ;;
    esac
done < <(git diff --name-only --diff-filter=U)

echo ""
echo "=================================="
echo "Conflict Resolution Summary"
echo "=================================="
git status
echo ""

# Check if all conflicts resolved
if git status | grep -q "both modified"; then
    echo "⚠️ Some conflicts still remain. Please resolve them manually."
    exit 1
else
    echo "✅ All conflicts resolved!"
    echo ""
    read -p "Complete merge with commit? (y/n): " complete
    if [ "$complete" = "y" ] || [ "$complete" = "Y" ]; then
        git commit -m "Merge master into digisign-prod - preserved signing implementation"
        echo ""
        echo "✅ Merge completed successfully!"
        echo ""
        echo "Next steps:"
        echo "1. Verify signing files: git diff digisign-prod-backup-<date> HEAD"
        echo "2. Build solution"
        echo "3. Test signing functionality"
        echo "4. Push to remote: git push origin digisign-prod"
    fi
fi
