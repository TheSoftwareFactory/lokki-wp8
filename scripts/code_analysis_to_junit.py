"""
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
"""
from xml.dom.minidom import getDOMImplementation

from os.path import basename, dirname, join, abspath
from optparse import OptionParser

THISDIR = dirname(abspath(__file__))

class TestResult(object):

    def __init__(self, message, level, classname, name):
        self.message = message
        self.level = level
        self.classname = classname
        self.name = name

def create_testsuite(doc, results):
    """ Generate testsuite for a single log

    <testsuite>
        <testcase classname="foo" name="ASuccessfulTest"/>
        <testcase classname="foo" name="AnotherSuccessfulTest"/>
        <testcase classname="foo" name="AFailingTest">
            <failure type="NotEnoughFoo"> details about failure </failure>
        </testcase>
    </testsuite>
    """

    suite = doc.createElement("testsuite")
    # top_element = newdoc.documentElement

    keys = results.keys();keys.sort()
    for key in keys:

        classname, name = key
        items = results[key]

        for result in items:
            #import pdb;pdb.set_trace()
            testcase = doc.createElement('testcase')
            testcase.setAttribute("classname", result.classname)
            testcase.setAttribute("name", name)

            failure = doc.createElement("failure")
            failure.setAttribute("type", result.level)
            errnode = doc.createTextNode(result.message)

            failure.appendChild(errnode)
            testcase.appendChild(failure)

            suite.appendChild(testcase)

    return suite

def generate(data):

    results = {}
    lines = data.split("\n")

    stripped_lines = set()
    for line in lines:
        line = line.strip()

        stripped = "[".join( line.split("[", )[:-1] ).strip()
        parts = stripped.split(": ")
        if len(parts) < 5: continue

        stripped_lines.add( line )

    for line in stripped_lines:
        parts = line.split(": ")

        level = parts[1].strip()
        name = parts[2].strip()
        classname = parts[3].strip()
        message = ": ".join(parts[4:])

        items = results.get( (classname, name), [] )
        items.append( TestResult( message, level, classname, name ) )
        results[(classname, name)] = items

    impl = getDOMImplementation()
    newdoc = impl.createDocument(None, "testsuites", None)

    testsuite = create_testsuite(newdoc, results)
    newdoc.documentElement.appendChild( testsuite )

    return newdoc

def to_xml_string(testsuites):

    data = testsuites.toprettyxml(indent = "  ")
    # toprettyxml adds extra new lines
    lines = [ x for x in data.split("\n") if len(x.strip()) > 0]

    return "\n".join( lines )


def main():
    parser = OptionParser( )
    parser.add_option("-s", "--source",
                      default = join(THISDIR, "..", "src","msbuild1.log"),
                      help = "Path to the log file")

    (options, args) = parser.parse_args()

    with open(options.source) as f:
        elems = generate(f.read())
        print to_xml_string(elems)

if __name__ == "__main__":
    main()
