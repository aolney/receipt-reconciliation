#!/bin/bash
FILES=*.docx
for f in $FILES
do
  # extension="${f##*.}"
  filename="${f%.*}"
  echo "Converting $f to $filename.txt"
  `pandoc "$f" -t plain -o "$filename.txt"`
  # uncomment this line to delete the source file.
  # rm $f
done

