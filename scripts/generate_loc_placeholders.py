"""
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
"""
#! coding: utf-8
"""
Tool for generating new string placeholders to localization files
"""

import sys
import glob

import xml.etree.ElementTree as ET
from xml.etree.ElementTree import ElementTree, Element
from os.path import join, basename, dirname, abspath, exists

THISDIR = dirname(abspath(__file__))


NEW_FIELD_TEMPLATE = """
  <data name="%(NAME)s" xml:space="preserve">
    <value>%(VALUE)s</value>
  </data>"""[1:] # remove newline

def parse(path):
    if exists(path):
        return ET.parse(path)

    return None;

def check_localized_resources(resources, defaults):

    ignored = []

    for resx in resources:

        filename = basename(resx)
        print filename

        dom = parse(resx)

        # First find all localization fields
        # Localization ID mapped to tag
        tags = {}
        for datatag in dom.findall("data"):

            name = datatag.get("name")

            if name == "ResourceLanguage":
                for t in datatag.getiterator("value"):
                    resourcelang = t.text

                    # Check the file name has correct language
                    if not resourcelang.startswith(filename.split(".")[-2]):
                        print "ERROR: ResourceLanguage '%s' not in filename '%s'" % ( resourcelang, filename )
                        #return False
                    break
            elif name == "ResourceFlowDirection":
                for t in datatag.getiterator("value"):
                    if t.text not in ["LeftToRight", "RightToLeft"]:
                        print "Invalid ResourceFlowDirection", t.text
                        return False
                    break

            elif name not in ignored: # Assuming the rest are localizations
                for t in datatag.getiterator("value"):
                    tags[name] = t
                    break

        newitems = []

        # Generate XML strings to be injected to the file to avoid messing it up
        # with etree's lack of formatter
        # Compare each language resource to default resource
        keys = defaults.keys();keys.sort()
        for label in keys:
            if label in tags:
                continue

            origtag = defaults[label]
            print "'%s' not found" % ( label )

            item = NEW_FIELD_TEMPLATE % {
                "NAME" : origtag.get("name"),
                "VALUE" : origtag.get("name").upper()
            }
            newitems.append( item )


        removed_tags = []
        update_needed = len(newitems)
        for t in tags:
            if t not in defaults:
                removed_tags.append( t )

        update_needed = len(newitems) + len(removed_tags)
        if update_needed == 0: continue

        # Inject the XML to avoid messing up the file
        data = ""
        with open(resx, 'r') as f:
            data = f.read()

            # Check obsolete fields and remove them
            for t in removed_tags:
                print "WARNING:'%s' not in use, removing." % t

                start = data.index('<data name="%s"' % t )
                end = '</data>\n'
                end = data.index(end, start ) + len(end)

                start = data[:start].rstrip()
                end = data[end:]
                data = start + "\n" + end

            i = data.index("</root>")
            newitems.append("</root>")
            data = data[:i] + "\n".join(newitems)

        with open(resx, 'wb') as f:
            f.write(data.replace("\r", "").replace("\n", "\r\n"))

    return True

def process(options):

    globformat = options.source.split(".")
    globformat = globformat[:-1] + ["*"] + globformat[-1:]
    globformat = ".".join(globformat)
    resources = glob.glob(globformat)

    print resources

    resx = parse(options.source)

    defaults = {}

    # Update the loc file with new fields from RESX
    # To detect removed fields
    resourcelang = None

    ignored = ["ResourceFlowDirection", "ResourceLanguage"]

    for datatag in resx.findall("data"):

        name = datatag.get("name")

        if name not in ignored:
            for t in datatag.getiterator("value"):
                value = t.text

                defaults[name] = datatag
                break

    return check_localized_resources(resources, defaults)

def main():
    from optparse import OptionParser

    parser = OptionParser()
    parser.add_option("-s", "--source",
                      default=join(THISDIR, "..", "Resources", "Localized.resx" ),
                      help="Path to default RESX")

    (options, args) = parser.parse_args()

    if not process(options):
        return sys.exit(-2)

if __name__ == "__main__":
    main()