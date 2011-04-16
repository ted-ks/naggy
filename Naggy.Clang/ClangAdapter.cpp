// This is the main DLL file.

#include "stdafx.h"
#include "ClangAdapter.h"

#include "clang/Basic/Diagnostic.h"

using namespace System::Runtime::InteropServices;
using namespace System::Collections::Generic;
using namespace NaggyClang;
using namespace std;

const char* ToCString(String ^str)
{
	System::IntPtr ptr = Marshal::StringToHGlobalAnsi(str);
	return (const char *)ptr.ToPointer();
}

String^ ToManagedString(const CXString &str)
{
	const char* strData = clang_getCString(str);
	
	String^ managedString = Marshal::PtrToStringAnsi(System::IntPtr((int)strData));
	return managedString;
}

Diagnostic^ ToDiagnostic(const CXDiagnostic &diag)
{
	Diagnostic^ managedDiag = gcnew Diagnostic;

	const CXString &diagMessage = clang_getDiagnosticSpelling(diag);
	managedDiag->Message = ToManagedString(diagMessage);
	clang_disposeString(diagMessage);

	
	CXSourceLocation start = clang_getDiagnosticLocation(diag);

	CXFile file; unsigned line, col, offset;
	clang_getInstantiationLocation(start, &file, &line, &col, &offset);

	CXString fileName = clang_getFileName(file);
	managedDiag->FilePath = ToManagedString(fileName);
	clang_disposeString(fileName);

	managedDiag->StartLine = line;
	managedDiag->StartColumn = col;

	return managedDiag;
}

List<Diagnostic^>^ ClangAdapter::GetDiagnostics(String ^contents)
{
	struct CXUnsavedFile mainFile;
	mainFile.Filename = m_filePath;
	mainFile.Contents = ToCString(contents);
	mainFile.Length = contents->Length;

	clang_reparseTranslationUnit(m_translationUnit, 1, &mainFile, 0);
	// Release char* created from contents.
	Marshal::FreeHGlobal(IntPtr((void *)mainFile.Contents));

	return GetDiagnostics();
}

List<Diagnostic^>^ ClangAdapter::GetDiagnostics()
{
	unsigned int numDiagnostics = clang_getNumDiagnostics(m_translationUnit);
	List<Diagnostic^>^ diagnostics = gcnew List<Diagnostic^>();	

	for (unsigned int i = 0; i<numDiagnostics; i++)
	{
		CXDiagnostic diag = clang_getDiagnostic(m_translationUnit, i);
		Diagnostic ^managedDiag = ToDiagnostic(diag);
		diagnostics->Add(managedDiag);
		clang_disposeDiagnostic(diag);
	}

	return diagnostics;
}

void ClangAdapter::Initialize(String ^filePath, List<String^> ^includePaths)
{
	CXIndex idx = clang_createIndex(0, 1);
	const int argsCount = includePaths->Count + 1;
	char **args = new char*[argsCount];

	int index = 0;
	for each(String^ path in includePaths)
	{
		args[index++] = (char*)ToCString("-I" + path);
	}
	args[index++] = "-fspell-checking";

	m_filePath = (char *) ToCString(filePath);
	m_translationUnit = clang_createTranslationUnitFromSourceFile(idx, m_filePath, argsCount, args, 0, NULL);
}